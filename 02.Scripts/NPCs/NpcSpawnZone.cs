using System;
using UnityEngine;
using IdleTycoon.Core;
using IdleTycoon.Economy;
using IdleTycoon.Items;
using IdleTycoon.Player;

namespace IdleTycoon.NPCs
{
    /// <summary>
    /// NPC 잠금 해제 존 — IInteractable 구현 (분할 납부 방식).
    ///
    /// MinerNPC / DeliveryNPC 등 어떤 NPC든 동일하게 사용 가능.
    /// npcSequence에 미리 비활성 상태로 배치된 GameObject를 할당하면
    /// 비용을 완납할 때마다 순서대로 SetActive(true)로 활성화한다.
    ///
    /// 흐름:
    ///   1. 플레이어 진입
    ///      - _remainingCost 초기화 (잔액 없을 때만)
    ///      - _sendCount = Min(보유 Money 개수, 남은 비용에 필요한 아이템 수)
    ///   2. paymentInterval마다 _sendCount > 0 이면 Money 1개 수거 → spawnOrigin으로 AssignSlot
    ///      → OnReachedTarget 콜백 → SpendMoney + _remainingCost 차감 → 완납 시 NPC 활성화
    ///   3. _remainingCost <= 0 → npcSequence[_currentIndex] 활성화 → 다음 NPC로 이동
    ///   4. 모든 NPC 활성화 완료 → gameObject.SetActive(false)
    ///
    /// _remainingCost 유지 / _sendCount 초기화:
    ///   _remainingCost — 존 이탈 시 보존, 다음 진입 시 이어서 납부.
    ///   _sendCount     — 존 이탈 시 0 초기화, 다음 진입 시 재계산.
    ///
    /// 씬 구성:
    ///   - npcSequence에 미리 배치된 비활성 GameObject (MinerNPC/DeliveryNPC 등) 할당
    ///   - spawnCosts 배열 크기 = npcSequence 크기 (부족 시 마지막 값으로 폴백)
    ///
    /// Hierarchy 구조:
    ///   [NpcSpawnZone]
    ///     ├─ NpcSpawnZone.cs
    ///     ├─ BoxCollider (isTrigger = true)
    ///     └─ [SpawnOrigin]  ← Money가 날아올 목표 위치 (선택)
    /// </summary>
    public class NpcSpawnZone : MonoBehaviour, IInteractable
    {
        // ─── Inspector ──────────────────────────────────────────────

        [Header("NPC Sequence")]
        [Tooltip("순서대로 활성화할 NPC GameObject 배열 (MinerNPC / DeliveryNPC 등).\n미리 씬에 배치 후 비활성 상태로 둘 것.")]
        [SerializeField] private GameObject[] npcSequence;

        [Tooltip("각 NPC 활성화 비용. 배열이 npcSequence보다 짧으면 마지막 값으로 폴백.")]
        [SerializeField] private int[] spawnCosts;

        [Header("Payment")]
        [Tooltip("Money 아이템 1개를 납부하는 간격 (초)")]
        [SerializeField] private float paymentInterval = 0.3f;

        [Tooltip("납부 Money가 날아갈 목표 위치 (없으면 Zone 중심)")]
        [SerializeField] private Transform spawnOrigin;

        [Tooltip("Money ItemData — 개당 sellValue로 필요 개수 계산에 사용")]
        [SerializeField] private ItemData moneyItemData;

        // ─── State ──────────────────────────────────────────────────

        private PlayerInteractor _interactor;
        private bool             _playerInZone;
        private float            _paymentTimer;
        private int              _currentIndex;   // 다음에 활성화할 NPC 인덱스

        /// <summary>현재 NPC 활성화까지 남은 납부 비용. 존 이탈 시 보존.</summary>
        private int _remainingCost;

        /// <summary>이번 방문에서 날릴 Money 아이템 수. 이탈 시 0 리셋.</summary>
        private int _sendCount;

        // ─── Public Properties ───────────────────────────────────────

        public int  RemainingCost  => _remainingCost;
        public int  CurrentIndex   => _currentIndex;
        public bool IsAllUnlocked  => npcSequence != null && _currentIndex >= npcSequence.Length;

        // ─── Lifecycle ──────────────────────────────────────────────

        private void Awake()
        {
            // 씬에 배치된 NPC가 활성 상태로 시작했다면 비활성화 보정
            if (npcSequence == null) return;
            foreach (var npc in npcSequence)
            {
                if (npc != null && npc.activeSelf)
                    npc.SetActive(false);
            }
        }

        private void Update()
        {
            HandlePayment();
        }

        // ─── IInteractable ──────────────────────────────────────────

        public void OnPlayerEnter(Component player)
        {
            if (player is not PlayerInteractor interactor) return;
            if (IsAllUnlocked) return;
            if (CurrencyManager.Instance == null) return;

            // 납부 잔액이 없으면 현재 NPC 비용으로 초기화
            if (_remainingCost <= 0)
            {
                int cost = GetCostForIndex(_currentIndex);
                if (cost <= 0) return;
                _remainingCost = cost;
            }

            _interactor   = interactor;
            _playerInZone = true;
            _paymentTimer = paymentInterval; // 진입 즉시 첫 납부

            // 이번 방문에서 날릴 개수 계산
            int perItem   = (moneyItemData != null && moneyItemData.sellValue > 0)
                            ? moneyItemData.sellValue : 1;
            int needed    = Mathf.CeilToInt((float)_remainingCost / perItem);
            int available = interactor.StackHolder.MoneyCount;
            _sendCount    = Mathf.Min(available, needed);
        }

        public void OnPlayerStay(Component player) { }  // Update에서 처리

        public void OnPlayerExit(Component player)
        {
            // _remainingCost 보존, _sendCount 초기화
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

            Transform target = spawnOrigin != null ? spawnOrigin : transform;
            gold.AssignSlot(target);

            Action callback = null;
            callback = () =>
            {
                gold.OnReachedTarget -= callback;
                _remainingCost       -= gold.Data.sellValue;
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
                ActivateNextNpc();
        }

        // ─── NPC 활성화 ──────────────────────────────────────────────

        private void ActivateNextNpc()
        {
            _remainingCost = 0;

            if (npcSequence == null || _currentIndex >= npcSequence.Length)
            {
                
                return;
            }

            GameObject npc = npcSequence[_currentIndex];
            if (npc != null)
            {
                npc.SetActive(true);
                
            }

            _currentIndex++;

            // 모든 NPC 활성화 완료 → 존 비활성화
            if (_currentIndex >= npcSequence.Length)
            {
                
                _playerInZone = false;
                _interactor   = null;
                _sendCount    = 0;
                gameObject.SetActive(false);
                return;
            }

            // 플레이어가 아직 존 안에 있으면 다음 NPC 납부를 즉시 이어서 준비
            if (_playerInZone && _interactor != null)
            {
                int cost = GetCostForIndex(_currentIndex);
                if (cost > 0)
                {
                    _remainingCost = cost;
                    int perItem   = (moneyItemData != null && moneyItemData.sellValue > 0)
                                    ? moneyItemData.sellValue : 1;
                    int needed    = Mathf.CeilToInt((float)_remainingCost / perItem);
                    int available = _interactor.StackHolder.MoneyCount;
                    _sendCount    = Mathf.Min(available, needed);
                }
            }
        }

        // ─── 유틸리티 ───────────────────────────────────────────────

        private int GetCostForIndex(int index)
        {
            if (spawnCosts == null || spawnCosts.Length == 0) return 0;
            int clamp = Mathf.Clamp(index, 0, spawnCosts.Length - 1);
            return spawnCosts[clamp];
        }

        // ─── Gizmo ──────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            bool paying = Application.isPlaying && _sendCount > 0;
            Gizmos.color = paying ? Color.yellow : Color.green;
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.1f, new Vector3(1.5f, 0.2f, 1.5f));

            if (spawnOrigin != null)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(spawnOrigin.position, 0.15f);
                Gizmos.DrawLine(transform.position, spawnOrigin.position);
            }

            // NPC 연결선 표시
            if (npcSequence == null) return;
            for (int i = 0; i < npcSequence.Length; i++)
            {
                if (npcSequence[i] == null) continue;
                bool isNext    = Application.isPlaying && i == _currentIndex;
                bool unlocked  = Application.isPlaying && i < _currentIndex;
                Gizmos.color   = unlocked ? Color.grey : isNext ? Color.cyan : Color.white;
                Gizmos.DrawLine(transform.position, npcSequence[i].transform.position);
                Gizmos.DrawWireSphere(npcSequence[i].transform.position, 0.4f);
            }

            // 납부 진행률 바 (에디터 플레이 중)
            if (!Application.isPlaying || _remainingCost <= 0 || IsAllUnlocked) return;
            int total = GetCostForIndex(_currentIndex);
            if (total > 0)
            {
                float ratio    = 1f - (float)_remainingCost / total;
                Gizmos.color   = Color.green;
                Gizmos.DrawWireCube(
                    transform.position + Vector3.up * 0.5f,
                    new Vector3(1.5f * ratio, 0.1f, 0.1f));
            }
        }
#endif
    }
}
