using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using IdleTycoon.Economy;
using IdleTycoon.Items;
using IdleTycoon.Machines;

namespace IdleTycoon.NPCs
{
    /// <summary>
    /// Handcuff 배달 NPC.
    ///
    /// 흐름:
    ///   GoToOutput      → MachineOutputZone 위치로 NavMesh 이동
    ///   WaitAtOutput    → OutputZone에 아이템이 생길 때까지 대기
    ///   Collecting      → min(available, capacity)개만큼 collectInterval마다 1개씩 수거
    ///                     수거 완료 시 GoToDelivery
    ///   GoToDelivery    → NpcDeliveryZone 위치로 이동
    ///   Delivering      → deliverInterval마다 1개씩 deliveryZone.ReceiveItem 호출
    ///                     전달 완료 시 WaitingDelivery
    ///   WaitingDelivery → deliveryZone.BufferedCount == 0 될 때까지 대기
    ///                     소진 완료 시 GoToOutput
    ///
    /// 슬롯 시스템:
    ///   stackOrigin (NPC 앞쪽 빈 Transform) 기준으로 Y축 슬롯을 Awake에 생성.
    ///   기존 StackItem.AssignSlot() + Lerp 이동 시스템 그대로 사용.
    ///
    /// Hierarchy:
    ///   [DeliveryNPC]
    ///     ├─ DeliveryNPC.cs
    ///     ├─ NavMeshAgent
    ///     └─ [StackOrigin]  ← NPC 앞쪽 빈 GameObject
    /// </summary>
    public class DeliveryNPC : MonoBehaviour
    {
        // ─── Inspector ──────────────────────────────────────────────

        [Header("References")]
        [Tooltip("아이템을 수거할 ProcessingMachine 출력 존")]
        [SerializeField] private MachineOutputZone outputZone;

        [Tooltip("Handcuff를 납품할 NPC 배달 존")]
        [SerializeField] private NpcDeliveryZone   deliveryZone;

        [Header("Stack")]
        [Tooltip("NPC 앞쪽 빈 Transform — 슬롯 기준점")]
        [SerializeField] private Transform stackOrigin;

        [Tooltip("최대 운반 가능 개수")]
        [SerializeField] private int       capacity    = 5;

        [Tooltip("슬롯 간 Y축 간격")]
        [SerializeField] private Vector3   stackOffset = new Vector3(0f, 0.25f, 0f);

        [Header("Timing")]
        [Tooltip("OutputZone에서 아이템 1개 수거 간격 (초)")]
        [SerializeField] private float collectInterval = 0.3f;

        [Tooltip("DeliveryZone에 아이템 1개 전달 간격 (초)")]
        [SerializeField] private float deliverInterval = 0.3f;

        [Header("Navigation")]
        [Tooltip("목표 위치까지의 도달 판정 거리")]
        [SerializeField] private float reachDistance = 1.5f;

        // ─── State ──────────────────────────────────────────────────

        private NavMeshAgent    _agent;
        private List<StackItem> _carriedItems = new();
        private List<Transform> _slots        = new();

        /// <summary>이번 수거 여행에서 가져올 목표 개수.</summary>
        private int   _collectTarget;
        private float _actionTimer;

        private enum State
        {
            GoToOutput,       // 출력존으로 이동
            WaitAtOutput,     // 아이템 생성 대기
            Collecting,       // 아이템 수거 중
            GoToDelivery,     // 배달존으로 이동
            Delivering,       // 아이템 전달 중
            WaitingDelivery,  // 버퍼 소진 대기
        }

        private State _state = State.GoToOutput;

        // ─── Lifecycle ──────────────────────────────────────────────

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            if (_agent != null)
                _agent.stoppingDistance = reachDistance;

            BuildSlots();
        }

        private void Start()
        {
            SetState(State.GoToOutput);
        }

        private void Update()
        {
            switch (_state)
            {
                case State.GoToOutput:      UpdateGoToOutput();      break;
                case State.WaitAtOutput:    UpdateWaitAtOutput();    break;
                case State.Collecting:      UpdateCollecting();      break;
                case State.GoToDelivery:    UpdateGoToDelivery();    break;
                case State.Delivering:      UpdateDelivering();      break;
                case State.WaitingDelivery: UpdateWaitingDelivery(); break;
            }
        }

        // ─── 상태 전환 ───────────────────────────────────────────────

        private void SetState(State next)
        {
            _state       = next;
            _actionTimer = 0f;

            switch (next)
            {
                case State.GoToOutput:
                    if (outputZone != null)
                        _agent.SetDestination(outputZone.transform.position);
                    break;

                case State.GoToDelivery:
                    if (deliveryZone != null)
                        _agent.SetDestination(deliveryZone.transform.position);
                    break;

                case State.Collecting:
                    _agent.ResetPath();
                    // 이번 여행 수거 목표 = min(현재 출력 개수, 빈 용량)
                    {
                        int avail  = outputZone != null ? outputZone.Machine.OutputCount : 0;
                        int space  = capacity - _carriedItems.Count;
                        _collectTarget = Mathf.Min(avail, space);
                    }
                    _actionTimer = collectInterval; // 첫 틱 즉시 실행
                    break;

                case State.Delivering:
                    _agent.ResetPath();
                    _actionTimer = deliverInterval; // 첫 틱 즉시 실행
                    break;

                case State.WaitAtOutput:
                case State.WaitingDelivery:
                    _agent.ResetPath();
                    break;
            }
        }

        // ─── 상태별 Update ──────────────────────────────────────────

        /// <summary>출력존 도착 → 아이템 있으면 수거, 없으면 대기.</summary>
        private void UpdateGoToOutput()
        {
            if (!HasArrived()) return;

            if (outputZone != null && outputZone.Machine.HasOutput)
                SetState(State.Collecting);
            else
                SetState(State.WaitAtOutput);
        }

        /// <summary>아이템이 생길 때까지 현장 대기.</summary>
        private void UpdateWaitAtOutput()
        {
            if (outputZone != null && outputZone.Machine.HasOutput)
                SetState(State.Collecting);
        }

        /// <summary>목표 개수까지 수거, 아이템 소진 시 바로 배달 출발.</summary>
        private void UpdateCollecting()
        {
            // 이번 여행 목표 수량 도달
            if (_carriedItems.Count >= _collectTarget)
            {
                if (_carriedItems.Count > 0)
                    SetState(State.GoToDelivery);
                else
                    SetState(State.WaitAtOutput); // target == 0 이면 재대기
                return;
            }

            // 아이템이 목표 전에 소진됨
            if (outputZone == null || !outputZone.Machine.HasOutput)
            {
                if (_carriedItems.Count > 0)
                    SetState(State.GoToDelivery);
                else
                    SetState(State.WaitAtOutput);
                return;
            }

            _actionTimer += Time.deltaTime;
            if (_actionTimer < collectInterval) return;
            _actionTimer = 0f;

            StackItem item = outputZone.Machine.TakeOutput();
            if (item == null) return;

            _carriedItems.Add(item);
            item.AssignSlot(_slots[_carriedItems.Count - 1]);
        }

        /// <summary>배달존 도착.</summary>
        private void UpdateGoToDelivery()
        {
            if (!HasArrived()) return;
            SetState(State.Delivering);
        }

        /// <summary>보유 아이템을 deliverInterval마다 1개씩 전달.</summary>
        private void UpdateDelivering()
        {
            if (_carriedItems.Count == 0)
            {
                SetState(State.WaitingDelivery);
                return;
            }

            if (deliveryZone == null || !deliveryZone.CanReceive()) return;

            _actionTimer += Time.deltaTime;
            if (_actionTimer < deliverInterval) return;
            _actionTimer = 0f;

            // 맨 위(마지막) 아이템부터 전달 (스택 LIFO)
            int       last = _carriedItems.Count - 1;
            StackItem item = _carriedItems[last];
            _carriedItems.RemoveAt(last);
            item.AssignSlot(null);

            deliveryZone.ReceiveItem(item);
        }

        /// <summary>배달존 버퍼가 비면 다시 출력존으로.</summary>
        private void UpdateWaitingDelivery()
        {
            if (deliveryZone == null || deliveryZone.BufferedCount == 0)
                SetState(State.GoToOutput);
        }

        // ─── 슬롯 생성 ───────────────────────────────────────────────

        private void BuildSlots()
        {
            if (stackOrigin == null) return;

            for (int i = 0; i < capacity; i++)
            {
                var go = new GameObject($"CarrySlot_{i}");
                go.transform.SetParent(stackOrigin);
                go.transform.localPosition = stackOffset * i;
                go.transform.localRotation = Quaternion.identity;
                _slots.Add(go.transform);
            }
        }

        // ─── 유틸리티 ───────────────────────────────────────────────

        private bool HasArrived()
        {
            if (_agent == null)         return true;
            if (_agent.pathPending)     return false;
            if (!_agent.hasPath)        return false;
            return _agent.remainingDistance <= reachDistance;
        }

        // ─── Gizmo ──────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Color col = _state switch
            {
                State.Collecting      => Color.yellow,
                State.Delivering      => Color.green,
                State.WaitAtOutput    => Color.grey,
                State.WaitingDelivery => Color.cyan,
                _                     => Color.white,
            };
            Gizmos.color = col;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 1.5f, 0.3f);

            if (outputZone != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position, outputZone.transform.position);
            }
            if (deliveryZone != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(transform.position, deliveryZone.transform.position);
            }
        }
#endif
    }
}
