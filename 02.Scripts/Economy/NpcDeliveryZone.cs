using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using IdleTycoon.Core;
using IdleTycoon.Items;
using IdleTycoon.Player;
using UnityEditor;

namespace IdleTycoon.Economy
{
    /// <summary>
    /// NPC 납품 존 — IInteractable 구현.
    ///
    /// 흐름:
    ///   1. 플레이어 진입 → transferInterval마다 두 작업 동시 처리:
    ///      a) 수거: StackHolder → deliveryOrigin 슬롯으로 Lerp 이동 (즉시 Despawn 없음)
    ///      b) 전달: deliveryOrigin 맨 앞 아이템 Despawn + _deliveredCount 증가
    ///   2. 플레이어 이탈 → 타이머·인터랙터 초기화, _deliveryBuffer 아이템은 유지
    ///   3. requiredHandcuffCount 도달 → 주문 완료 → MoneyItem 스폰 + 이벤트 발행
    ///
    /// deliveryOrigin 슬롯 구조:
    ///   ProcessingMachine.BuildInputSlots()과 동일하게 Transform 슬롯을 사전 생성.
    ///   수거된 StackItem은 AssignSlot()으로 해당 슬롯으로 Lerp 이동한다.
    ///   전달(FIFO)시 나머지 슬롯 재배정 → 자연스럽게 앞으로 Lerp 이동.
    ///
    /// Hierarchy 구조:
    ///   [NpcDeliveryZone]
    ///     ├─ NpcDeliveryZone.cs
    ///     ├─ BoxCollider (isTrigger = true)
    ///     ├─ [DeliveryOrigin]   ← 수거 아이템이 쌓일 기준 위치
    ///     └─ [MoneyOutputPoint] ← 주문 완료 시 MoneyItem 스폰 위치
    /// </summary>
    public class NpcDeliveryZone : MonoBehaviour, IInteractable
    {
        // ─── Inspector ──────────────────────────────────────────────

        [Header("Delivery Settings")]
        [Tooltip("StackHolder → deliveryOrigin 수거 간격 (초)")]
        [SerializeField] private float collectInterval  = 0.3f;

        [Tooltip("deliveryOrigin → 전달(Despawn+카운트) 간격 (초)")]
        [SerializeField] private float deliveryInterval = 0.5f;

        [Tooltip("주문 완료에 필요한 Handcuff 총 개수")]
        private int requiredHandcuffCount = 5;

        [Header("Delivery Origin")]
        [Tooltip("수거한 아이템이 Lerp로 이동해 쌓일 기준 위치")]
        [SerializeField] private Transform deliveryOrigin;
        [SerializeField] private Vector3   deliverySlotOffset  = new Vector3(0f, 0.25f, 0f);
        [SerializeField] private int       maxDeliveryCapacity = 10;

        [Header("Reward")]
        [Tooltip("주문 완료 시 지급되는 재화량")]
        [SerializeField] private int rewardAmount = 100;

        [Tooltip("ItemType.Money 로 설정된 ItemData (prefab 포함)")]
        [SerializeField] private ItemData moneyItemData;

        [Tooltip("주문 완료 시 MoneyItem이 스폰될 월드 위치")]
        [SerializeField] private Transform moneyOutputPoint;

        [Tooltip("스폰된 MoneyItem을 관리할 MoneyOutputZone. 연결 시 플레이어가 직접 수령해야 돈이 지급된다.")]
        [SerializeField] private MoneyOutputZone moneyOutputZone;

        [Tooltip("전달된 Handcuff가 Lerp로 날아갈 NPC Transform (예: NPC 머리 위 빈 오브젝트)")]
        [SerializeField] private Transform npcTargetTransform;

        // ─── State ──────────────────────────────────────────────────

        private PlayerInteractor _interactor;
        private bool             _playerInZone;
        private float            _collectTimer;
        private float            _deliveryTimer;
        private int              _deliveredCount;

        /// <summary>NpcQueue가 비면 false — 수거·전달 모두 중단.</summary>
        private bool _queueActive = true;

        private readonly List<StackItem> _deliveryBuffer = new();   // deliveryOrigin에 쌓인 아이템
        private readonly List<Transform> _deliverySlots  = new();   // deliveryOrigin 슬롯 Transforms

        // ─── Public Properties ───────────────────────────────────────

        public int   DeliveredCount        => _deliveredCount;
        public int   RequiredHandcuffCount => requiredHandcuffCount;
        public int   BufferedCount         => _deliveryBuffer.Count;
        public float OrderProgress         => requiredHandcuffCount > 0
                                           ? (float)_deliveredCount / requiredHandcuffCount
                                           : 1f;

        // ─── Lifecycle ──────────────────────────────────────────────

        private void Awake()
        {
            BuildDeliverySlots();
        }

        private void Update()
        {
            HandleDelivery();
        }

        // ─── IInteractable ──────────────────────────────────────────

        public void OnPlayerEnter(Component player)
        {
            if (player is not PlayerInteractor interactor) return;
            _interactor    = interactor;
            _playerInZone  = true;
            _collectTimer  = collectInterval;  // 진입 즉시 첫 수거
            _deliveryTimer = deliveryInterval; // 진입 즉시 첫 전달
        }

        public void OnPlayerStay(Component player) { }  // Update에서 처리

        public void OnPlayerExit(Component player)
        {
            // _deliveryBuffer 아이템은 유지 — 다음 진입 시 이어서 전달
            _playerInZone  = false;
            _interactor    = null;
            _collectTimer  = 0f;
            _deliveryTimer = 0f;
        }

        // ─── NpcQueue API ────────────────────────────────────────────

        /// <summary>NpcQueue가 맨 앞 NPC의 요구 수량을 주입한다.</summary>
        public void SetRequiredCount(int count)
        {
            requiredHandcuffCount = Mathf.Max(1, count);
            _deliveredCount       = 0;
            _queueActive          = true;
        }

        /// <summary>NpcQueue의 NPC가 모두 소진됐을 때 납품을 중단한다.</summary>
        public void SetQueueEmpty()
        {
            _queueActive = false;
        }

        // ─── NPC 수신 API ────────────────────────────────────────────

        /// <summary>
        /// DeliveryNPC가 운반한 아이템을 deliveryOrigin 슬롯으로 직접 전달한다.
        /// </summary>
        public bool CanReceive() => _deliveryBuffer.Count < maxDeliveryCapacity;

        /// <summary>
        /// DeliveryNPC가 아이템을 하나씩 밀어 넣을 때 호출.
        /// 슬롯 배정 → Lerp 이동 → 자동 TryDeliver 처리.
        /// </summary>
        public void ReceiveItem(StackItem item)
        {
            if (item == null) return;
            _deliveryBuffer.Add(item);
            
            if (_deliverySlots.Count >= _deliveryBuffer.Count)
                item.AssignSlot(_deliverySlots[_deliveryBuffer.Count - 1]);
        }

        // ─── 수거 & 전달 처리 ────────────────────────────────────────

        private void HandleDelivery()
        {
            if (!_queueActive) return;

            // 수거: 플레이어가 존 안에 있을 때만
            if (_playerInZone && _interactor != null)
            {
                _collectTimer += Time.deltaTime;
                if (_collectTimer >= collectInterval)
                {
                    _collectTimer = 0f;
                    TryCollect();
                }
            }

            // 전달: 버퍼에 아이템이 있으면 플레이어 유무와 무관하게 자동 처리
            // (DeliveryNPC가 ReceiveItem으로 쌓은 아이템도 소진)
            if (_deliveryBuffer.Count > 0)
            {
                _deliveryTimer += Time.deltaTime;
                if (_deliveryTimer >= deliveryInterval)
                {
                    _deliveryTimer = 0f;
                    TryDeliver();
                }
            }
        }

        /// <summary>StackHolder에서 Handcuff 1개를 deliveryOrigin 슬롯으로 이동.</summary>
        private void TryCollect()
        {
            if (_deliveryBuffer.Count >= maxDeliveryCapacity)              return;
            if (!_interactor.StackHolder.HasItemOfType(ItemType.Handcuff)) return;
            
            StackItem item = _interactor.StackHolder.ProvideItemOfType(ItemType.Handcuff);
            if (item == null) return;
            
            AudioManager.Instance.PlayGetItemSFX();
            _deliveryBuffer.Add(item);
            
            item.transform.localScale = Vector3.zero;
            item.transform.DOScale(Vector3.one * 2f,0.15f).SetEase(Ease.OutQuad)
                .OnComplete(()=>item.transform.DOScale(Vector3.one,0.1f));

            if (_deliverySlots.Count >= _deliveryBuffer.Count)
                item.AssignSlot(_deliverySlots[_deliveryBuffer.Count - 1]);
        }

        /// <summary>deliveryOrigin 맨 앞(FIFO) 아이템을 npcTargetTransform으로 Lerp 전송.
        /// 도달 시 OnReachedTarget 콜백 → Despawn(풀 반환) + deliveredCount 증가.</summary>
        private void TryDeliver()
        {
            if (_deliveryBuffer.Count == 0) return;

            StackItem item = _deliveryBuffer[0];
            _deliveryBuffer.RemoveAt(0);

            // 나머지 아이템 슬롯 한 칸 앞으로 재배정 → Lerp로 이동
            for (int i = 0; i < _deliveryBuffer.Count; i++)
                _deliveryBuffer[i].AssignSlot(_deliverySlots[i]);

            // npcTargetTransform으로 Lerp 이동, 도달 시 콜백에서 처리
            item.AssignSlot(npcTargetTransform);
            item.OnReachedTarget += () =>
            {
                AudioManager.Instance.PlayGiveItemSFX();
                item.Despawn();
                _deliveredCount++;
                if (_deliveredCount >= requiredHandcuffCount)
                    CompleteOrder();
            };
        }

        // ─── 주문 완료 ───────────────────────────────────────────────

        private void CompleteOrder()
        {
            SpawnMoneyAtOutput();

            if (moneyOutputZone == null && CurrencyManager.Instance != null)
                CurrencyManager.Instance.AddMoneyRaw(rewardAmount);

            EventBus.RaiseOrderCompleted(rewardAmount);

            _deliveredCount = 0;
        }

        private void SpawnMoneyAtOutput()
        {
            if (moneyItemData == null || moneyItemData.prefab == null)
            {
                
                return;
            }

            Transform spawnTr = moneyOutputPoint != null ? moneyOutputPoint : transform;

            StackItem item = StackItemPool.Instance != null
                ? StackItemPool.Instance.Get(moneyItemData, spawnTr.position)
                : FallbackInstantiate(moneyItemData, spawnTr.position);

            if (item == null) return;
            item.transform.rotation = spawnTr.rotation;
            moneyOutputZone?.AddMoneyItem(item);
        }

        // ─── Delivery 슬롯 생성 ──────────────────────────────────────

        /// <summary>
        /// deliveryOrigin 기준으로 maxDeliveryCapacity개의 Transform 슬롯을 사전 생성.
        /// ProcessingMachine.BuildInputSlots()과 동일한 패턴.
        /// </summary>
        private void BuildDeliverySlots()
        {
            if (deliveryOrigin == null) return;

            for (int i = 0; i < maxDeliveryCapacity; i++)
            {
                var slotGo = new GameObject($"DeliverySlot_{i}");
                slotGo.transform.SetParent(deliveryOrigin);
                slotGo.transform.localPosition = deliverySlotOffset * i;
                slotGo.transform.localRotation = Quaternion.identity;
                _deliverySlots.Add(slotGo.transform);
            }
        }

        private StackItem FallbackInstantiate(ItemData data, Vector3 pos)
        {
            GameObject go   = Instantiate(data.prefab, pos, Quaternion.identity);
            StackItem  item = go.GetComponent<StackItem>();
            if (item == null) { Destroy(go); return null; }
            item.Spawn(data, null);
            return item;
        }

        // ─── Gizmo ──────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.1f, new Vector3(1.5f, 0.2f, 1.5f));

            if (deliveryOrigin != null)
            {
                Gizmos.color = Color.cyan;
                for (int i = 0; i < maxDeliveryCapacity; i++)
                    Gizmos.DrawWireSphere(deliveryOrigin.position + deliverySlotOffset * i, 0.08f);
            }

            if (moneyOutputPoint != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(moneyOutputPoint.position, 0.25f);
                Gizmos.DrawLine(transform.position, moneyOutputPoint.position);
            }
        }
#endif
    }
}
