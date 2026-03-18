using System.Collections.Generic;
using UnityEngine;

namespace IdleTycoon.Items
{
    /// <summary>
    /// StackItem 오브젝트 풀 — 싱글턴.
    ///
    /// ItemType 기준으로 풀을 분리 관리한다.
    /// Instantiate 대신 Get(), Despawn 대신 Release()를 사용한다.
    ///
    /// 흐름:
    ///   Get(data, position)
    ///     → 해당 ItemType 큐에 재사용 가능한 StackItem이 있으면 꺼내 Spawn 후 반환
    ///     → 없으면 data.prefab을 Instantiate → Spawn 후 반환
    ///
    ///   Release(item)
    ///     → item.ResetState() (슬롯·콜백·Data 전부 초기화 + SetActive false)
    ///     → 해당 ItemType 큐에 반환
    ///
    /// Prewarm:
    ///   prewarmEntries에 ItemData + count를 등록하면
    ///   Awake에서 미리 오브젝트를 생성해 풀에 적재한다.
    ///
    /// Hierarchy:
    ///   씬에 빈 GameObject를 만들고 StackItemPool 컴포넌트를 부착.
    /// </summary>
    public class StackItemPool : MonoBehaviour
    {
        // ─── Singleton ──────────────────────────────────────────────

        public static StackItemPool Instance { get; private set; }

        // ─── Inspector ──────────────────────────────────────────────

        [Header("Prewarm (선택)")]
        [Tooltip("게임 시작 시 미리 생성할 ItemData + 개수. 없으면 런타임에 필요할 때 생성.")]
        [SerializeField] private PrewarmEntry[] prewarmEntries;
        [SerializeField] private Transform poolParent;

        [System.Serializable]
        public class PrewarmEntry
        {
            public ItemData data;
            [Min(0)] public int count = 5;
        }

        // ─── Pool Storage ────────────────────────────────────────────

        /// <summary>ItemType 별 풀 큐. Release 시 큐에 반환, Get 시 큐에서 꺼낸다.</summary>
        private readonly Dictionary<ItemType, Queue<StackItem>> _pools = new();

        // ─── Lifecycle ──────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            
            foreach (var e in prewarmEntries) 
            {
                Prewarm(e.data, e.count);
            }
        }

        // ─── Public API ──────────────────────────────────────────────

        /// <summary>
        /// ItemType 풀에서 StackItem을 꺼내 spawnPosition에 배치 후 반환한다.
        /// 풀이 비어 있으면 data.prefab을 Instantiate한다.
        /// </summary>
        public StackItem Get(ItemData data, Vector3 spawnPosition)
        {
            
            if (data == null)
            {
               
                return null;
            }
            if (data.prefab == null)
            {
                
                return null;
            }

            StackItem item = null;

            if (_pools.TryGetValue(data.itemType, out var queue) && queue.Count > 0)
            {
                item = queue.Dequeue();
                item.transform.position = spawnPosition;
                item.transform.rotation = Quaternion.identity;
            }
            else
            {
                GameObject go = Instantiate(data.prefab, spawnPosition, Quaternion.identity,poolParent);
                item = go.GetComponent<StackItem>();

                if (item == null)
                {
                    
                    Destroy(go);
                    return null;
                }
            }

            item.Spawn(data, null);
            return item;
        }

        /// <summary>
        /// StackItem을 풀에 반환한다.
        /// item.ResetState()로 모든 런타임 상태를 초기화한 뒤 큐에 넣는다.
        /// </summary>
        public void Release(StackItem item)
        {
            if (item == null) return;
            
            ItemType type = item.ItemType;     // ResetState 전에 캡처

            item.ResetState();                       // Data = null
            item.gameObject.SetActive(false);

            if (!_pools.TryGetValue(type, out var queue))
            {
                queue = new Queue<StackItem>();
                _pools[type] = queue;
            }
            item.transform.SetParent(poolParent);
            queue.Enqueue(item);
        }

        // ─── 풀 상태 조회 ────────────────────────────────────────────

        /// <summary>특정 ItemType의 현재 풀 크기.</summary>
        public int PoolSize(ItemType type)
            => _pools.TryGetValue(type, out var q) ? q.Count : 0;

        // ─── Prewarm ─────────────────────────────────────────────────

        private void Prewarm(ItemData data, int count)
        {
            if (data == null || data.prefab == null || count <= 0) return;

            if (!_pools.TryGetValue(data.itemType, out var queue))
            {
                queue = new Queue<StackItem>();
                _pools[data.itemType] = queue;
            }

            for (int i = 0; i < count; i++)
            {
                GameObject go   = Instantiate(data.prefab, Vector3.zero, Quaternion.identity,poolParent);
                StackItem  item = go.GetComponent<StackItem>();

                if (item == null)
                {
                    
                    Destroy(go);
                    continue;
                }

                // Data를 임시 설정 후 ResetState로 비활성화 상태로 풀에 적재
                item.Spawn(data, null);
                item.ResetState();
                item.gameObject.SetActive(false);
                queue.Enqueue(item);
            }

            
        }
    }
}
