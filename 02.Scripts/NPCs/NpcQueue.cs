using System.Collections.Generic;
using UnityEngine;
using IdleTycoon.Core;
using IdleTycoon.Economy;

namespace IdleTycoon.NPCs
{
    /// <summary>
    /// NPC 줄서기 관리자.
    ///
    /// 흐름:
    ///   1. Start: QueueNpcPool.Get으로 npcCount개 NPC 생성 → _activeQueue 등록 → AssignAllSlots
    ///   2. 맨 앞(index 0) NPC의 RequiredCount를 NpcDeliveryZone에 주입
    ///   3. EventBus.OnOrderCompleted 수신 시:
    ///      a. 맨 앞 NPC → Depart(exitPoint)  → Depart 완료 시 NPC가 스스로 Pool.Release
    ///      b. Pool.Get으로 새 NPC 생성 → _activeQueue 맨 뒤 추가
    ///      c. AssignAllSlots — 나머지 NPC 한 칸 앞, 새 NPC 맨 뒤 슬롯으로 Lerp
    ///      d. PushFrontRequirement — 새 맨 앞 NPC 요구 수량 DeliveryZone에 전달
    ///
    /// AssignAllSlots 로직 원본 유지:
    ///   activeQueue[i] → slots[i] 순서로 1:1 배정.
    ///   slots 수보다 많은 NPC는 슬롯 미배정(대기 상태).
    ///
    /// Hierarchy 구조:
    ///   [NpcQueue]
    ///     ├─ NpcQueue.cs
    ///     ├─ [Slot_0]     ← 맨 앞 (납품 위치)
    ///     ├─ [Slot_1]
    ///     ├─ [Slot_2]
    ///     ├─ [ExitPoint]  ← 퇴장 NPC 목적지
    ///     └─ [SpawnPoint] ← 신규 NPC 등장 위치 (선택, 없으면 ExitPoint 사용)
    /// </summary>
    public class NpcQueue : MonoBehaviour
    {
        // ─── Inspector ──────────────────────────────────────────────

        [Header("Queue Layout")]
        [Tooltip("슬롯 배열. index 0 = 맨 앞 (납품 위치)")]
        [SerializeField] private Transform[] slots;

        [Tooltip("퇴장 NPC가 이동할 목적지")]
        [SerializeField] private Transform exitPoint;

        [Tooltip("새 NPC가 등장할 위치. 미설정 시 ExitPoint를 사용.")]
        [SerializeField] private Transform spawnPoint;

        [Header("NPCs")]
        [Tooltip("생성에 사용할 QueueNpc 프리팹")]
        [SerializeField] private QueueNpc npcPrefab;

        [Tooltip("초기 큐 인원 수")]
        [SerializeField] private int npcCount = 3;

        [Header("References")]
        [SerializeField] private NpcDeliveryZone deliveryZone;

        // ─── State ──────────────────────────────────────────────────

        private readonly List<QueueNpc> _activeQueue = new();

        // ─── Lifecycle ──────────────────────────────────────────────

        private void Start()
        {
            SpawnInitialQueue();
            AssignAllSlots();
            PushFrontRequirement();
        }

        private void OnEnable()  => EventBus.OnOrderCompleted += HandleOrderCompleted;
        private void OnDisable() => EventBus.OnOrderCompleted -= HandleOrderCompleted;

        // ─── 초기 큐 생성 ────────────────────────────────────────────

        private void SpawnInitialQueue()
        {
            if (QueueNpcPool.Instance == null)
            {
                
                return;
            }

            for (int i = 0; i < npcCount; i++) {
                Vector3 spawnPos;
                
                spawnPos = i<slots.Length ? slots[i].position : GetSpawnPosition();
                
                QueueNpc npc = QueueNpcPool.Instance.Get(npcPrefab, spawnPos);
                
                if (npc != null)
                    _activeQueue.Add(npc);
            }
        }

        // ─── EventBus 핸들러 ─────────────────────────────────────────

        private void HandleOrderCompleted(int reward)
        {
            if (_activeQueue.Count == 0) return;

            // 맨 앞 NPC 퇴장 (Depart 완료 시 NPC 스스로 Pool.Release)
            QueueNpc frontNpc = _activeQueue[0];
            _activeQueue.RemoveAt(0);
            frontNpc.Depart(exitPoint);

            // 풀에서 새 NPC를 꺼내 큐 맨 뒤에 추가
            if (QueueNpcPool.Instance != null)
            {
                QueueNpc newNpc = QueueNpcPool.Instance.Get(npcPrefab, GetSpawnPosition());
                if (newNpc != null)
                    _activeQueue.Add(newNpc);
            }

            // 나머지 NPC 한 칸 앞 + 새 NPC 맨 뒤 슬롯 배정 → 자동 Lerp
            AssignAllSlots();

            // 새 맨 앞 NPC 요구 수량 전달
            PushFrontRequirement();
        }

        // ─── Slot 관리 ──────────────────────────────────────────────

        /// <summary>activeQueue[i] → slots[i] 순서로 슬롯 재배정. (원본 로직 유지)</summary>
        private void AssignAllSlots()
        {
            for (int i = 0; i < _activeQueue.Count; i++)
            {
                QueueNpc npc = _activeQueue[i];
                
                if (i < slots.Length) 
                {
                    npc.gameObject.SetActive(true);
                    npc.AssignSlot(slots[i]);
                }
                else 
                {
                    npc.gameObject.SetActive(false);
                }
            }
        }

        // ─── DeliveryZone 연동 ──────────────────────────────────────

        private void PushFrontRequirement()
        {
            if (deliveryZone == null) return;

            if (_activeQueue.Count > 0)
                deliveryZone.SetRequiredCount(_activeQueue[0].RequiredCount);
            else
                deliveryZone.SetQueueEmpty();
        }

        // ─── 유틸리티 ───────────────────────────────────────────────

        private Vector3 GetSpawnPosition()
        {
            if (spawnPoint != null) return spawnPoint.position;
            if (exitPoint  != null) return exitPoint.position;
            return transform.position;
        }

        // ─── Gizmo ──────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (slots == null) return;

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null) continue;
                Gizmos.color = (i == 0) ? Color.red : Color.yellow;
                Gizmos.DrawWireSphere(slots[i].position, 0.2f);
                if (i > 0 && slots[i - 1] != null)
                    Gizmos.DrawLine(slots[i - 1].position, slots[i].position);
            }

            if (exitPoint != null)
            {
                Gizmos.color = Color.grey;
                Gizmos.DrawWireSphere(exitPoint.position, 0.2f);
            }

            if (spawnPoint != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(spawnPoint.position, 0.2f);
                Gizmos.DrawLine(transform.position, spawnPoint.position);
            }
        }
#endif
    }
}
