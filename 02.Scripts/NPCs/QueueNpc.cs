using UnityEngine;
using Random = UnityEngine.Random;


namespace IdleTycoon.NPCs
{
    /// <summary>
    /// 큐에 서는 개별 NPC.
    ///
    /// NpcQueue가 AssignSlot()으로 목표 슬롯을 지정하면 moveSpeed로 Lerp 이동한다.
    /// Depart() 호출 시 exitPoint로 이동 후 QueueNpcPool.Release (풀 없으면 SetActive false).
    ///
    /// 풀링 대응:
    ///   _maxRequired — 프리팹 원본 최댓값. Awake에서 캡처 후 풀 재사용 시에도 유지.
    ///   Reinitialize() — 풀에서 꺼낼 때 호출. 요구 수량 재난수화 + 이동 상태 초기화.
    /// </summary>
    public class QueueNpc : MonoBehaviour
    {
        // ─── Inspector ──────────────────────────────────────────────

        [Header("Order")]
        [Tooltip("요구 Handcuff 수량 상한. 실제값은 Awake/Reinitialize에서 Random.Range(2, 이 값)으로 결정.")]
        [SerializeField] private int requiredHandcuffCount = 5;

        [Header("Movement")]
        [SerializeField] private float moveSpeed = 5f;

        [Tooltip("슬롯 위치에 도달한 것으로 판단하는 거리 임계값")]
        [SerializeField] private float arrivalThreshold = 0.05f;

        // ─── State ──────────────────────────────────────────────────

        private Transform _targetTransform;
        private bool      _isDeparting;

        /// <summary>프리팹 원본 최댓값 — 풀 재사용 시 재난수화 기준으로 사용.</summary>
        private int _maxRequired;

        // ─── Public Properties ───────────────────────────────────────

        public int RequiredCount => requiredHandcuffCount;

        // ─── Public API ─────────────────────────────────────────────

        /// <summary>NpcQueue가 호출 — 목표 슬롯을 지정하고 일반 이동 모드로 전환.</summary>
        public void AssignSlot(Transform slot)
        {
            _targetTransform = slot;
            _isDeparting     = false;
        }

        /// <summary>NpcQueue가 호출 — exitPoint로 이동 후 풀 반환.</summary>
        public void Depart(Transform exitPoint)
        {
            _targetTransform = exitPoint;
            _isDeparting     = true;
        }

        /// <summary>
        /// 풀에서 꺼낼 때 QueueNpcPool이 호출.
        /// 요구 수량 재난수화 + 이동 상태 초기화 + 활성화.
        /// </summary>
        public void Reinitialize(Vector3 spawnPosition)
        {
            requiredHandcuffCount = Random.Range(2, _maxRequired+1);
            _targetTransform      = null;
            _isDeparting          = false;
            transform.position    = spawnPosition;
            gameObject.SetActive(true);
        }

        // ─── Lifecycle ──────────────────────────────────────────────

        private void Awake()
        {
            // 프리팹 원본 최댓값 캡처 (풀 재사용 시에도 유지됨)
            _maxRequired          = requiredHandcuffCount;
        }

        private void Update()
        {
            if (_targetTransform == null) return;

            transform.position = Vector3.MoveTowards(
                    transform.position, _targetTransform.position
                    ,
                    moveSpeed * Time.deltaTime
            );

            if (_isDeparting &&
                Vector3.Distance(transform.position, _targetTransform.position) < arrivalThreshold)
            {
                if (QueueNpcPool.Instance != null)
                    QueueNpcPool.Instance.Release(this);
                else
                    gameObject.SetActive(false);
            }
        }

        // ─── Gizmo ──────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_targetTransform == null) return;
            Gizmos.color = _isDeparting ? Color.red : Color.cyan;
            Gizmos.DrawLine(transform.position, _targetTransform.position);
        }
#endif
    }
}
