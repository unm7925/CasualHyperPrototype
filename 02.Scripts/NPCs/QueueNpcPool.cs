using System.Collections.Generic;
using UnityEngine;

namespace IdleTycoon.NPCs
{
    /// <summary>
    /// QueueNpc 오브젝트 풀 — 싱글턴.
    ///
    /// StackItemPool과 동일한 구조.
    ///   Get(prefab, position)  — 풀에 재사용 가능한 NPC가 있으면 꺼내 Reinitialize,
    ///                            없으면 prefab을 Instantiate 후 Reinitialize.
    ///   Release(npc)           — SetActive(false) 후 풀 큐에 반환.
    ///
    /// QueueNpc.Depart 이동 완료 시 내부에서 Release를 자동 호출하므로
    /// 외부(NpcQueue)는 Release를 직접 호출할 필요가 없다.
    ///
    /// Hierarchy:
    ///   씬에 빈 GameObject를 만들고 QueueNpcPool 컴포넌트를 부착.
    ///   Script Execution Order에서 NpcQueue보다 먼저 실행되도록 설정 권장.
    /// </summary>
    public class QueueNpcPool : MonoBehaviour
    {
        // ─── Singleton ──────────────────────────────────────────────

        public static QueueNpcPool Instance { get; private set; }

        // ─── Inspector ──────────────────────────────────────────────

        [Header("Pool Parent (선택)")]
        [Tooltip("풀에 반환된 NPC의 부모 Transform. 없으면 루트에 배치.")]
        [SerializeField] private Transform poolParent;

        // ─── Pool Storage ────────────────────────────────────────────

        private readonly Queue<QueueNpc> _pool = new();

        // ─── Lifecycle ──────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        // ─── Public API ──────────────────────────────────────────────

        /// <summary>
        /// 풀에서 QueueNpc를 꺼내 spawnPosition에 배치 후 반환한다.
        /// 풀이 비어 있으면 prefab을 Instantiate한다.
        /// </summary>
        public QueueNpc Get(QueueNpc prefab, Vector3 spawnPosition)
        {
            QueueNpc npc;

            if (_pool.Count > 0)
            {
                npc = _pool.Dequeue();
            }
            else
            {
                if (prefab == null)
                {
                   
                    return null;
                }
                Transform parent = poolParent != null ? poolParent : null;
                npc = parent != null
                    ? Instantiate(prefab, spawnPosition, Quaternion.identity, parent)
                    : Instantiate(prefab, spawnPosition, Quaternion.identity);
            }
            npc.gameObject.SetActive(true);
            npc.Reinitialize(spawnPosition);
            return npc;
        }

        /// <summary>
        /// QueueNpc를 풀에 반환한다.
        /// QueueNpc.Update에서 Depart 이동 완료 시 자동 호출된다.
        /// </summary>
        public void Release(QueueNpc npc)
        {
            if (npc == null) return;
            
            npc.gameObject.SetActive(false);

            if (poolParent != null)
                npc.transform.SetParent(poolParent);

            _pool.Enqueue(npc);
        }

        // ─── 풀 상태 조회 ────────────────────────────────────────────

        public int PoolSize => _pool.Count;
    }
}
