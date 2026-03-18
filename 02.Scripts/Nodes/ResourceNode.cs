using System.Collections;
using UnityEngine;
using IdleTycoon.Player;

namespace IdleTycoon.Nodes
{
    /// <summary>
    /// 채굴 노드.
    ///
    /// 역할 1 — 리스폰 관리:
    ///   Junk.Mined 이벤트 구독 → 리스폰 코루틴 시작
    ///
    /// 역할 2 — 채굴 장비 스폰/디스폰:
    ///   BoxCollider(isTrigger=true)로 플레이어 진입/이탈 감지
    ///   → PlayerInteractor.SpawnMiningEquipment / DespawnMiningEquipment 호출
    ///
    /// 채굴 활성화는 PlayerInteractor + MiningToolTrigger가 담당한다.
    ///
    /// Hierarchy 구조:
    ///   [ResourceNode]
    ///     ├─ ResourceNode.cs
    ///     ├─ BoxCollider (isTrigger = true)  ← 플레이어 진입/이탈용
    ///     ├─ [Junk_0]  ← ResourceJunk + Collider (Junk 레이어)
    ///     ├─ [Junk_1]
    ///     └─ [Junk_2]
    /// </summary>
    public class ResourceNode : MonoBehaviour
    {
        // ─── Inspector ──────────────────────────────────────────────

        [Header("Data")]
        [SerializeField] private NodeData nodeData;

        [Header("Junks")]
        [Tooltip("비워두면 자식 오브젝트에서 자동 수집")]
        [SerializeField] private ResourceJunk[] junks;
        
        

        // ─── Lifecycle ──────────────────────────────────────────────

        private void Awake()
        {
            if (junks == null || junks.Length == 0)
                junks = GetComponentsInChildren<ResourceJunk>();

            foreach (var junk in junks)
                junk.Mined += OnJunkMined;
        }

        private void OnDestroy()
        {
            foreach (var junk in junks)
                if (junk != null) junk.Mined -= OnJunkMined;
        }
        public ResourceJunk[] GetJunks() => junks;

        // ─── 장비 스폰/디스폰 — 플레이어 진입/이탈 ─────────────────

        private void OnTriggerEnter(Collider other)
        {
            // MiningTool 레이어 콜라이더는 무시 (장비 자체가 트리거하지 않도록)
            if (other.gameObject.layer == LayerMask.NameToLayer("MiningTool")) return;

            PlayerInteractor interactor = other.GetComponentInParent<PlayerInteractor>();
            if (interactor != null)
                interactor.SpawnMiningEquipment();
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.gameObject.layer == LayerMask.NameToLayer("MiningTool")) return;

            PlayerInteractor interactor = other.GetComponentInParent<PlayerInteractor>();
            if (interactor != null)
                interactor.DespawnMiningEquipment();
        }

        // ─── Respawn ────────────────────────────────────────────────

        private void OnJunkMined(ResourceJunk junk)
        {
            StartCoroutine(RespawnCoroutine(junk));
        }

        private IEnumerator RespawnCoroutine(ResourceJunk junk)
        {
            yield return new WaitForSeconds(nodeData.respawnTime);
            junk.Respawn();
        }

        // ─── Gizmo ──────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 0.6f);
        }
#endif
    }
}
