using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using IdleTycoon.Core;
using IdleTycoon.Items;

namespace IdleTycoon.Player
{
    /// <summary>
    /// 플레이어(또는 NPC)의 아이템 스택 시스템.
    ///
    /// 방향 구분:
    ///   Front (앞) — Handcuff 전용 단일 열
    ///   Back  (뒤) — Stone 1열 고정 / Money는 Stone 있으면 2열, 없으면 1열
    ///
    /// IItemReceiver  : 외부(ResourceNode, ProcessingMachine)가 아이템을 건넬 때 사용.
    /// IItemProvider  : 외부(SellZone, ProcessingMachine)가 아이템을 가져갈 때 사용.
    /// </summary>
    public class StackHolder : MonoBehaviour, IItemReceiver, IItemProvider
    {
        // ─── Inspector — Front Stack (Handcuff) ─────────────────────

        [Header("Front Stack — Handcuff")]
        [Tooltip("Handcuff 스택의 기준 Transform (플레이어 앞쪽)")]
        [SerializeField] private Transform stackOriginFront;

        [SerializeField] private int     maxFrontCapacity = 10;
        [SerializeField] private Vector3 frontStackOffset = new Vector3(0f, 0.3f, 0f);
        [SerializeField] private Vector3 frontDepthOffset = new Vector3(0f, 0f,  0.1f);

        // ─── Inspector — Back Stack (Stone / Money) ──────────────────

        [Header("Back Stack — Stone / Money")]
        [Tooltip("Stone·Money 스택의 기준 Transform (플레이어 뒤쪽)")]
        [SerializeField] private Transform stackOriginBack;

        [SerializeField] private int     maxStoneCapacity = 10;
        [SerializeField] private int     maxMoneyCapacity = 5;
        [SerializeField] private Vector3 backStackOffset  = new Vector3(0f, 0.3f,  0f);
        [SerializeField] private Vector3 backDepthOffset  = new Vector3(0f, 0f,  -0.1f);

        [Tooltip("Money 2열 오프셋: Stone이 1개 이상 있을 때 Money 열을 이동시키는 방향")]
        [SerializeField] private Vector3 backColumnOffset = new Vector3(0.35f, 0f, 0f);

        // ─── State ──────────────────────────────────────────────────

        private readonly List<StackItem> _handcuffStack = new();
        private readonly List<StackItem> _stoneStack    = new();
        private readonly List<StackItem> _moneyStack    = new();

        private readonly List<Transform> _frontSlots = new();  // Handcuff 슬롯
        private readonly List<Transform> _stoneSlots = new();  // Stone 슬롯 (back col1 고정)
        private readonly List<Transform> _moneySlots = new();  // Money 슬롯 (col 동적)

        // ─── Public Properties ──────────────────────────────────────

        public int  HandcuffCount  => _handcuffStack.Count;
        public int  StoneCount     => _stoneStack.Count;
        public int  MoneyCount     => _moneyStack.Count;
        public int  Count          => _handcuffStack.Count + _stoneStack.Count + _moneyStack.Count;

        public int  MaxFrontCapacity => maxFrontCapacity;
        public int  MaxCapacity      => maxFrontCapacity + maxStoneCapacity;

        public bool IsHandcuffFull => _handcuffStack.Count >= maxFrontCapacity;
        public bool IsStoneFull    => _stoneStack.Count    >= maxStoneCapacity;
        public bool IsMoneyFull    => _moneyStack.Count    >= maxMoneyCapacity;

        /// <summary>Handcuff·Stone 양쪽이 모두 찼을 때 true.</summary>
        public bool IsFull  => IsHandcuffFull && IsStoneFull;
        public bool IsEmpty => _handcuffStack.Count == 0 && _stoneStack.Count == 0 && _moneyStack.Count == 0;

        // ─── Lifecycle ──────────────────────────────────────────────

        private void Awake()
        {
            RebuildAllSlots();
        }

        // ─── IItemReceiver ──────────────────────────────────────────

        /// <summary>아이템 타입 별로 해당 스택 용량을 확인한다.</summary>
        public bool CanReceive(ItemData itemData)
        {
            return itemData.itemType switch
            {
                ItemType.Handcuff => !IsHandcuffFull,
                ItemType.Stone    => !IsStoneFull,
                ItemType.Money    => !IsMoneyFull,
                _                 => false
            };
        }

        public void Receive(StackItem item)
        {
            if (!CanReceive(item.Data))
            {
                
                return;
            }

            switch (item.Data.itemType)
            {
                case ItemType.Handcuff:
                    _handcuffStack.Add(item);
                    item.AssignSlot(_frontSlots[_handcuffStack.Count - 1]);
                    AudioManager.Instance.PlayGetItemSFX();
                    break;

                case ItemType.Stone:
                    _stoneStack.Add(item);
                    item.AssignSlot(_stoneSlots[_stoneStack.Count - 1]);
                    // Stone이 생기면 Money 열을 col2로 이동
                    RefreshMoneyColumn();
                    break;

                case ItemType.Money:
                    _moneyStack.Add(item);
                    item.AssignSlot(_moneySlots[_moneyStack.Count - 1]);
                    AudioManager.Instance.PlayMoneyGainSFX();
                    break;
            }
            
            item.transform.localScale = Vector3.zero;
            item.transform.DOScale(Vector3.one * 2f,0.15f).SetEase(Ease.OutQuad)
                .OnComplete(()=>item.transform.DOScale(Vector3.one,0.1f));
            
            EventBus.RaiseItemStacked(item);
            EventBus.RaiseStackCountChanged(Count, MaxCapacity);
        }

        // ─── IItemProvider ──────────────────────────────────────────

        public bool HasItems() => !IsEmpty;

        /// <summary>우선순위: Handcuff → Stone → Money.</summary>
        public StackItem ProvideItem()
        {
            if (_handcuffStack.Count > 0) return ProvideItemOfType(ItemType.Handcuff);
            if (_stoneStack.Count    > 0) return ProvideItemOfType(ItemType.Stone);
            if (_moneyStack.Count    > 0) return ProvideItemOfType(ItemType.Money);
            return null;
        }

        /// <summary>특정 ItemType의 맨 위 아이템을 꺼낸다.</summary>
        public StackItem ProvideItemOfType(ItemType type)
        {
            List<StackItem>  stack = GetStack(type);
            List<Transform>  slots = GetSlots(type);

            if (stack.Count == 0) return null;

            int       last = stack.Count - 1;
            StackItem item = stack[last];
            stack.RemoveAt(last);
            item.AssignSlot(null);

            // Stone이 줄면 Money 열 위치 재계산
            if (type == ItemType.Stone) RefreshMoneyColumn();

            RefreshSlots(slots, stack);

            EventBus.RaiseItemRemoved(item);
            EventBus.RaiseStackCountChanged(Count, MaxCapacity);
            return item;
        }

        public bool HasItemOfType(ItemType type) => GetStack(type).Count > 0;

        // ─── Capacity Upgrade ────────────────────────────────────────

        /// <summary>업그레이드 존이 호출하여 최대 용량을 늘린다.</summary>
        public void UpgradeCapacity(int newFrontMax, int newStoneMax)
        {
            maxFrontCapacity = newFrontMax;
            maxStoneCapacity += newStoneMax;
            RebuildAllSlots();
        }

        // ─── Slot Management ─────────────────────────────────────────

        private void RebuildAllSlots()
        {
            Vector3 moneyColOff = _stoneStack.Count > 0 ? backColumnOffset : Vector3.zero;

            RebuildSlots(_frontSlots, stackOriginFront, maxFrontCapacity,
                         frontStackOffset, frontDepthOffset, Vector3.zero);

            RebuildSlots(_stoneSlots, stackOriginBack, maxStoneCapacity,
                         backStackOffset, backDepthOffset, Vector3.zero);

            RebuildSlots(_moneySlots, stackOriginBack, maxMoneyCapacity,
                         backStackOffset, backDepthOffset, moneyColOff);

            RefreshSlots(_frontSlots, _handcuffStack);
            RefreshSlots(_stoneSlots, _stoneStack);
            RefreshSlots(_moneySlots, _moneyStack);
        }

        private static void RebuildSlots(List<Transform> slots, Transform origin,
                                          int capacity, Vector3 stackOff, Vector3 depthOff,
                                          Vector3 colOff)
        {
            foreach (var slot in slots)
                if (slot != null) Destroy(slot.gameObject);
            slots.Clear();

            if (origin == null) return;

            for (int i = 0; i < capacity; i++)
            {
                var slotGo = new GameObject($"Slot_{i}");
                slotGo.transform.SetParent(origin);
                slotGo.transform.localPosition = stackOff * i + depthOff * i + colOff;
                slotGo.transform.localRotation = Quaternion.identity;
                slots.Add(slotGo.transform);
            }
        }

        /// <summary>Stone 수가 바뀔 때 Money 슬롯의 열(column) 위치를 재계산.</summary>
        private void RefreshMoneyColumn()
        {
            Vector3 colOff = _stoneStack.Count > 0 ? backColumnOffset : Vector3.zero;
            for (int i = 0; i < _moneySlots.Count; i++)
            {
                if (_moneySlots[i] == null) continue;
                _moneySlots[i].localPosition = backStackOffset * i + backDepthOffset * i + colOff;
            }
            // Money 아이템이 이미 쌓여 있으면 새 슬롯 위치로 재배정
            RefreshSlots(_moneySlots, _moneyStack);
        }

        private static void RefreshSlots(List<Transform> slots, List<StackItem> stack)
        {
            for (int i = 0; i < stack.Count; i++)
                stack[i].AssignSlot(slots[i]);
        }

        // ─── Helpers ────────────────────────────────────────────────

        private List<StackItem> GetStack(ItemType type) => type switch
        {
            ItemType.Handcuff => _handcuffStack,
            ItemType.Stone    => _stoneStack,
            ItemType.Money    => _moneyStack,
            _                 => _handcuffStack
        };

        private List<Transform> GetSlots(ItemType type) => type switch
        {
            ItemType.Handcuff => _frontSlots,
            ItemType.Stone    => _stoneSlots,
            ItemType.Money    => _moneySlots,
            _                 => _frontSlots
        };

        // ─── Debug Gizmos ────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            DrawGizmos(stackOriginFront, maxFrontCapacity,
                       frontStackOffset, frontDepthOffset, Vector3.zero, Color.cyan);

            DrawGizmos(stackOriginBack, maxStoneCapacity,
                       backStackOffset, backDepthOffset, Vector3.zero, Color.yellow);

            Vector3 moneyColOff = Application.isPlaying && _stoneStack.Count > 0
                                  ? backColumnOffset : Vector3.zero;
            DrawGizmos(stackOriginBack, maxMoneyCapacity,
                       backStackOffset, backDepthOffset, moneyColOff, Color.green);
        }

        private static void DrawGizmos(Transform origin, int cap,
                                        Vector3 stackOff, Vector3 depthOff,
                                        Vector3 colOff, Color color)
        {
            if (origin == null) return;
            Gizmos.color = color;
            for (int i = 0; i < cap; i++)
            {
                Vector3 pos = origin.position
                            + origin.TransformDirection(stackOff * i + depthOff * i + colOff);
                Gizmos.DrawWireSphere(pos, 0.1f);
            }
        }
#endif
    }
}
