using System.Collections.Generic;
using UnityEngine;
using IdleTycoon.Core;
using IdleTycoon.Items;
using IdleTycoon.Player;

namespace IdleTycoon.Economy
{
    /// <summary>
    /// 머니 아이템 수령 존 — IInteractable 구현.
    ///
    /// 흐름:
    ///   1. NpcDeliveryZone.CompleteOrder() → AddMoneyItem(item) 로 아이템 등록
    ///   2. 플레이어가 존 진입 + StackHolder.CanReceive 확인
    ///   3. transferInterval마다 _moneyItems 에서 1개 꺼내 StackHolder.Receive()
    ///   4. CurrencyManager.AddMoneyRaw(item.Data.sellValue) 호출
    ///   5. 목록이 비면 동작 중지
    ///
    /// 시각 정렬:
    ///   zoneOrigin 기준으로 itemStackOffset * i 위치에 아이템을 직접 배치한다.
    ///   StackItem에 슬롯이 없으므로(_isFollowing=false) 직접 이동이 가능하다.
    ///
    /// Hierarchy 구조:
    ///   [MoneyOutputZone]
    ///     ├─ MoneyOutputZone.cs
    ///     ├─ BoxCollider (isTrigger = true)
    ///     └─ [ZoneOrigin]  ← 아이템이 쌓일 기준 위치
    /// </summary>
    public class MoneyOutputZone : MonoBehaviour, IInteractable
    {
        // ─── Inspector ──────────────────────────────────────────────

        [Header("Pickup Settings")]
        [Tooltip("아이템 1개를 플레이어에게 전달하는 간격 (초)")]
        [SerializeField] private float transferInterval = 0.2f;

        [Header("Zone Visual Layout")]
        [Tooltip("아이템이 쌓일 기준 위치. 없으면 자신의 Transform 사용.")]
        [SerializeField] private Transform zoneOrigin;

        [Tooltip("아이템 하나당 쌓이는 오프셋")]
        [SerializeField] private Vector3 itemStackOffset = new Vector3(0f, 0.25f, 0f);

        // ─── State ──────────────────────────────────────────────────

        private readonly List<StackItem> _moneyItems = new();

        private PlayerInteractor _interactor;
        private bool             _playerInZone;
        private float            _transferTimer;

        // ─── Public Properties ───────────────────────────────────────

        public bool HasItems => _moneyItems.Count > 0;
        public int  ItemCount => _moneyItems.Count;

        // ─── Public API — NpcDeliveryZone 연동 ──────────────────────

        /// <summary>
        /// NpcDeliveryZone이 주문 완료 시 생성한 MoneyItem을 이 존에 등록한다.
        /// 등록된 아이템은 zoneOrigin 기준 스택 위치에 배치된다.
        /// </summary>
        public void AddMoneyItem(StackItem item)
        {
            if (item == null) return;
            _moneyItems.Add(item);
            RefreshZonePositions();
        }

        // ─── Lifecycle ──────────────────────────────────────────────

        private void Update()
        {
            HandlePickup();
        }

        // ─── IInteractable ──────────────────────────────────────────

        public void OnPlayerEnter(Component player)
        {
            if (player is not PlayerInteractor interactor) return;
            _interactor    = interactor;
            _playerInZone  = true;
            _transferTimer = transferInterval; // 진입 즉시 첫 수령
        }

        public void OnPlayerStay(Component player) { }  // Update에서 처리

        public void OnPlayerExit(Component player)
        {
            _playerInZone  = false;
            _interactor    = null;
            _transferTimer = 0f;
        }

        // ─── 수령 처리 ───────────────────────────────────────────────

        private void HandlePickup()
        {
            if (!_playerInZone || _interactor == null) return;
            if (_moneyItems.Count == 0)                return;

            // 맨 위 아이템의 타입으로 수용 가능 여부 확인
            StackItem topItem = _moneyItems[^1];
            if (!_interactor.StackHolder.CanReceive(topItem.Data)) return;

            _transferTimer += Time.deltaTime;
            if (_transferTimer < transferInterval) return;

            _transferTimer = 0f;

            // 목록에서 제거 후 StackHolder로 전달
            _moneyItems.RemoveAt(_moneyItems.Count - 1);

            if (CurrencyManager.Instance != null)
                CurrencyManager.Instance.AddMoneyRaw(topItem.Data.sellValue);

            _interactor.StackHolder.Receive(topItem);
            // Receive 내부에서 슬롯 할당 → StackItem이 플레이어 스택으로 Lerp 이동

            RefreshZonePositions();
        }

        // ─── 존 내 시각 정렬 ─────────────────────────────────────────

        /// <summary>
        /// 남아있는 아이템을 zoneOrigin 기준으로 재배치한다.
        /// StackItem은 슬롯이 없으므로(_isFollowing = false) transform.position을 직접 조작한다.
        /// </summary>
        private void RefreshZonePositions()
        {
            Transform origin = zoneOrigin != null ? zoneOrigin : transform;
            for (int i = 0; i < _moneyItems.Count; i++)
            {
                if (_moneyItems[i] == null) continue;
                _moneyItems[i].transform.position = origin.position + itemStackOffset * i;
                _moneyItems[i].transform.rotation = origin.rotation;
            }
        }

        // ─── Gizmo ──────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.1f, new Vector3(1.5f, 0.2f, 1.5f));

            if (zoneOrigin == null) return;
            Gizmos.color = Color.yellow;
            for (int i = 0; i < 3; i++)
                Gizmos.DrawWireSphere(zoneOrigin.position + itemStackOffset * i, 0.1f);
        }
#endif
    }
}
