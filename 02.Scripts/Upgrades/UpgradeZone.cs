using System;
using UnityEngine;
using IdleTycoon.Core;
using IdleTycoon.Economy;
using IdleTycoon.Items;
using IdleTycoon.Player;

namespace IdleTycoon.Upgrades
{
    /// <summary>
    /// 업그레이드 존 — IInteractable 구현 (분할 납부 방식).
    ///
    /// 흐름:
    ///   1. 플레이어 진입
    ///      - _remainingCost 초기화 (잔액 없을 때만)
    ///      - _sendCount = Min(보유 Money 개수, 남은 비용에 필요한 아이템 수)
    ///   2. paymentInterval마다 _sendCount > 0 이면 Money 1개 수거 → upgradeOrigin으로 AssignSlot
    ///      → OnReachedTarget 콜백 → SpendMoney + _remainingCost 차감 → Despawn
    ///   3. _sendCount == 0 또는 Money 없으면 납부 중단
    ///   4. _remainingCost <= 0 → UpgradeManager.TryUpgrade()
    ///   5. 최대 레벨 도달 → gameObject.SetActive(false)
    ///
    /// _remainingCost 유지 / _sendCount 초기화:
    ///   _remainingCost — 존 이탈 시 보존, 다음 진입 시 이어서 납부.
    ///   _sendCount     — 존 이탈 시 0 초기화, 다음 진입 시 재계산.
    ///
    /// Hierarchy 구조:
    ///   [UpgradeZone]
    ///     ├─ UpgradeZone.cs
    ///     ├─ BoxCollider (isTrigger = true)
    ///     └─ [UpgradeOrigin]  ← Money가 날아올 목표 위치 (선택)
    /// </summary>
    public class UpgradeZone : MonoBehaviour, IInteractable
    {
        // ─── Inspector ──────────────────────────────────────────────

        [Header("Upgrade Settings")]
        [Tooltip("이 존이 담당하는 업그레이드 종류")]
        [SerializeField] private UpgradeType upgradeType = UpgradeType.MiningTool;

        [Tooltip("Money 아이템 1개를 납부하는 간격 (초)")]
        [SerializeField] private float paymentInterval = 0.3f;

        [Tooltip("납부 Money가 날아갈 목표 위치 (없으면 Zone 중심)")]
        [SerializeField] private Transform upgradeOrigin;

        [Tooltip("Money ItemData — 개당 sellValue로 필요 개수 계산에 사용")]
        [SerializeField] private ItemData moneyItemData;

        // ─── State ──────────────────────────────────────────────────

        private PlayerInteractor _interactor;
        private bool             _playerInZone;
        private float            _paymentTimer;

        /// <summary>
        /// 현재 업그레이드까지 남은 납부 비용.
        /// 플레이어가 zone을 나가도 유지되며, 다음 진입 시 이어서 납부한다.
        /// </summary>
        private int _remainingCost;

        /// <summary>
        /// 이번 방문에서 날릴 Money 아이템 수.
        /// 진입 시 Min(보유 개수, 필요 개수)로 초기화, 이탈 시 0 리셋.
        /// </summary>
        private int _sendCount;

        // ─── Public Properties ───────────────────────────────────────

        public int  RemainingCost => _remainingCost;
        public bool IsPaying      => _playerInZone && _sendCount > 0;

        // ─── Lifecycle ──────────────────────────────────────────────

        private void Update()
        {
            HandlePayment();
        }

        // ─── IInteractable ──────────────────────────────────────────

        public void OnPlayerEnter(Component player)
        {
            if (player is not PlayerInteractor interactor) return;
            if (UpgradeManager.Instance == null)                  return;
            if (!UpgradeManager.Instance.CanUpgrade(upgradeType)) return;

            // 납부 잔액이 없으면 이번 레벨 비용으로 초기화
            if (_remainingCost <= 0)
            {
                int cost = UpgradeManager.Instance.GetNextCost(upgradeType);
                if (cost <= 0) return;
                if (CurrencyManager.Instance == null) return;
                _remainingCost = cost;
            }

            _interactor   = interactor;
            _playerInZone = true;
            _paymentTimer = paymentInterval; // 진입 즉시 첫 납부

            // 이번 방문에서 날릴 개수 계산
            int perItem  = (moneyItemData != null && moneyItemData.sellValue > 0)
                           ? moneyItemData.sellValue : 1;
            int needed   = Mathf.CeilToInt((float)_remainingCost / perItem);
            int available = interactor.StackHolder.MoneyCount;
            _sendCount   = Mathf.Min(available, needed);
        }

        public void OnPlayerStay(Component player) { }  // Update에서 처리

        public void OnPlayerExit(Component player)
        {
            // _remainingCost 는 유지 — 다음 진입 시 이어서 납부
            // _sendCount 초기화 — 다음 진입 시 잔액 기준으로 재계산
            _playerInZone = false;
            _interactor   = null;
            _paymentTimer = 0f;
            _sendCount    = 0;
        }

        // ─── 분할 납부 처리 ──────────────────────────────────────────

        private void HandlePayment()
        {
            if (!_playerInZone || _interactor == null) return;
            if (_sendCount <= 0)                       return;

            _paymentTimer += Time.deltaTime;
            if (_paymentTimer < paymentInterval) return;

            _paymentTimer = 0f;

            StackItem gold = _interactor.StackHolder.ProvideItemOfType(ItemType.Money);
            if (gold == null) return;

            _sendCount--;

            // upgradeOrigin으로 날리기 + 도달 콜백
            Transform target = upgradeOrigin != null ? upgradeOrigin : transform;
            gold.AssignSlot(target);

            Action callback = null;
            callback = () =>
            {
                gold.OnReachedTarget -= callback;
                _remainingCost -= gold.Data.sellValue;
                OnGoldArrived(gold);
            };
            gold.OnReachedTarget += callback;
        }

        // ─── 납부 도달 처리 ──────────────────────────────────────────

        private void OnGoldArrived(StackItem item)
        {
            CurrencyManager.Instance?.SpendMoney(item.Data.sellValue);
            item.Despawn();

            if (_remainingCost <= 0)
                ExecuteUpgrade();
        }

        // ─── 업그레이드 실행 ─────────────────────────────────────────

        private void ExecuteUpgrade()
        {
            _remainingCost = 0;

            if (UpgradeManager.Instance == null) return;

            bool success = UpgradeManager.Instance.TryUpgrade(upgradeType);

            if (!success)
            {
                
                return;
            }

            // 최대 레벨 도달 시 존 비활성화
            if (!UpgradeManager.Instance.CanUpgrade(upgradeType))
            {
                
                gameObject.SetActive(false);
            }
        }

        // ─── Gizmo ──────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            bool paying = Application.isPlaying && _sendCount > 0;
            Gizmos.color = paying ? Color.yellow : Color.cyan;
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.1f, new Vector3(1.5f, 0.2f, 1.5f));

            if (upgradeOrigin != null)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(upgradeOrigin.position, 0.15f);
                Gizmos.DrawLine(transform.position, upgradeOrigin.position);
            }

            // 납부 진행률 바 (에디터 플레이 중)
            if (Application.isPlaying && _remainingCost > 0 && UpgradeManager.Instance != null)
            {
                int total = UpgradeManager.Instance.GetNextCost(upgradeType);
                if (total > 0)
                {
                    float ratio = 1f - (float)_remainingCost / total;
                    Gizmos.color = Color.green;
                    Gizmos.DrawWireCube(
                        transform.position + Vector3.up * 0.5f,
                        new Vector3(1.5f * ratio, 0.1f, 0.1f));
                }
            }
        }
#endif
    }
}
