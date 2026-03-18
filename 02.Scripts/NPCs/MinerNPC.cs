using System.Collections;
using IdleTycoon.Core;
using UnityEngine;
using UnityEngine.AI;
using IdleTycoon.Items;
using IdleTycoon.Machines;
using IdleTycoon.Nodes;

namespace IdleTycoon.NPCs
{
    /// <summary>
    /// 자율 채굴 NPC.
    ///
    /// 흐름:
    ///   Searching → 가장 가까운 비점유 ResourceJunk 탐색 + TryClaim
    ///   Moving    → NavMeshAgent로 정크 위치 이동
    ///              (이동 중 정크 비활성화/탈취 감지 시 즉시 재탐색)
    ///   Mining    → ResourceJunk.StartMining(NPC 오버로드) 호출
    ///   OnItemProduced → ProcessingMachine.ReceiveItem → inputOrigin 슬롯으로 Lerp
    ///   OnJunkMined    → 재탐색
    ///
    /// 다중 NPC:
    ///   TryClaim/Unclaim으로 정크 선점 → 여러 NPC가 같은 정크를 중복 타겟하지 않음.
    ///
    /// 스포너 연동:
    ///   Init(machine, junks) 호출 후 Start에서 채굴 시작.
    ///   씬에 직접 배치 시 자동으로 FindObjectsOfType 사용.
    ///
    /// Hierarchy:
    ///   [MinerNPC]
    ///     ├─ MinerNPC.cs
    ///     ├─ NavMeshAgent
    ///     └─ (메시/애니메이터 등)
    /// </summary>
    public class MinerNPC : MonoBehaviour
    {
        // ─── Inspector ──────────────────────────────────────────────

        [Header("References")]
        [Tooltip("채굴한 아이템을 전달할 ProcessingMachine")]
        [SerializeField] private ProcessingMachine targetMachine;
        [SerializeField] private ResourceNode targetNode;

        [Header("Settings")]
        [Tooltip("정크에 이 거리 이하로 접근 시 채굴 시작")]
        [SerializeField] private float reachDistance    = 1.5f;
        [Tooltip("주변 정크가 없을 때 재탐색 간격 (초)")]
        [SerializeField] private float searchRetryDelay = 1f;
        [Tooltip("이 NPC의 채굴 속도. ResourceJunk 채굴 간격 = mineInterval / mineSpeed")]
        [Min(0.01f)]
        [SerializeField] private float mineSpeed        = 1f;

        // ─── State ──────────────────────────────────────────────────

        private NavMeshAgent  _agent;
        private ResourceJunk[] _allJunks;
        private ResourceJunk   _targetJunk;

        private enum State { Searching, Moving, Mining, Idle }
        private State _state = State.Searching;

        // ─── Lifecycle ──────────────────────────────────────────────

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _agent.stoppingDistance = reachDistance;
        }

        private void Start()
        {
            if (_allJunks == null || _allJunks.Length == 0) 
            {
                _allJunks = targetNode != null ? targetNode.GetJunks() : FindObjectsOfType<ResourceJunk>();
            }
            
            SearchNextJunk();
        }

        private void Update()
        {
            if (_state != State.Moving) return;

            ValidateTarget();

            if (_state == State.Moving)
                CheckArrival();
        }

        // ─── 스포너 API ─────────────────────────────────────────────

        /// <summary>
        /// 스포너가 인스턴스 생성 후 참조를 주입한다.
        /// junks를 null로 전달하면 Start에서 자동 탐색.
        /// </summary>
        public void Init(ProcessingMachine machine, ResourceJunk[] junks = null)
        {
            targetMachine = machine;
            if (junks != null && junks.Length > 0)
                _allJunks = junks;
        }

        // ─── 탐색 ───────────────────────────────────────────────────

        private void SearchNextJunk()
        {
            _targetJunk = FindNearestUnclaimed();

            if (_targetJunk == null)
            {
                _state = State.Idle;
                StartCoroutine(RetrySearchCoroutine());
                return;
            }

            _state = State.Moving;
            _agent.SetDestination(_targetJunk.transform.position);
        }

        private void ValidateTarget()
        {
            if (_targetJunk == null ||
                !_targetJunk.IsAvailable ||
                (_targetJunk.IsClaimed && !_targetJunk.IsClaimedBy(this)))
            {
                // 정크가 비활성화되거나 다른 주체가 탈취 → 클레임 해제 후 재탐색
                _targetJunk?.Unclaim(this);
                _agent.ResetPath();
                _targetJunk = null;
                _state      = State.Searching;
                SearchNextJunk();
            }
        }

        private void CheckArrival()
        {
            if (_agent.pathPending) return;
            if (!_agent.hasPath)    return;
            if (_agent.remainingDistance > reachDistance) return;

            _agent.ResetPath();
            StartMiningCurrentJunk();
        }

        // ─── 채굴 ───────────────────────────────────────────────────

        private void StartMiningCurrentJunk()
        {
            if (_targetJunk == null || !_targetJunk.IsAvailable)
            {
                _targetJunk = null;
                _state      = State.Searching;
                SearchNextJunk();
                return;
            }

            bool started = _targetJunk.StartMining(this, OnItemProduced, OnJunkMined, mineSpeed);
            if (!started)
            {
                // 도착 직전 다른 주체에게 탈취됨 → 재탐색
                _targetJunk = null;
                _state      = State.Searching;
                SearchNextJunk();
                return;
            }

            _state = State.Mining;
        }

        // ─── 채굴 콜백 ──────────────────────────────────────────────

        /// <summary>채굴 완료 — StackItem을 ProcessingMachine에 전달.</summary>
        private void OnItemProduced(StackItem item)
        {
            if (item == null) return;
            AudioManager.Instance.PlayMineNPCSFX();
            if (targetMachine == null || !targetMachine.ReceiveItem(item))
            {
                // 머신 없거나 가득 참 → 아이템 제거
                item.Despawn();
                
            }
        }

        /// <summary>정크 비활성화 콜백 — 다음 정크 탐색.</summary>
        private void OnJunkMined(ResourceJunk junk)
        {
            _targetJunk = null;
            _state      = State.Searching;
            SearchNextJunk();
        }

        // ─── 유틸리티 ───────────────────────────────────────────────

        /// <summary>가장 가까운 비점유 정크를 찾아 클레임 선점 후 반환.</summary>
        private ResourceJunk FindNearestUnclaimed()
        {
            ResourceJunk nearest = null;
            float        minDist = float.MaxValue;

            foreach (var junk in _allJunks)
            {
                if (junk == null || !junk.IsAvailable)                           continue;
                if (junk.IsClaimed && !junk.IsClaimedBy(this))                   continue;

                float dist = Vector3.Distance(transform.position, junk.transform.position);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = junk;
                }
            }

            // 선택한 정크 클레임 선점 (동시 탐색 중 다른 NPC 차단)
            if (nearest != null && !nearest.TryClaim(this))
                nearest = null; // 동시 접근 시 실패 → 다음 탐색에서 재시도

            return nearest;
        }

        private IEnumerator RetrySearchCoroutine()
        {
            yield return new WaitForSeconds(searchRetryDelay);
            if (_state == State.Idle)
            {
                _state = State.Searching;
                SearchNextJunk();
            }
        }

        // ─── Gizmo ──────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_targetJunk == null) return;

            Gizmos.color = _state switch
            {
                State.Moving  => Color.cyan,
                State.Mining  => Color.yellow,
                _             => Color.grey
            };
            Gizmos.DrawLine(transform.position, _targetJunk.transform.position);
            Gizmos.DrawWireSphere(_targetJunk.transform.position, 0.3f);
        }
#endif
    }
}
