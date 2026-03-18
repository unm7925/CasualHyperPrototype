using UnityEngine;
using IdleTycoon.Nodes;
using IdleTycoon.Player;

namespace IdleTycoon.Upgrades
{
    /// <summary>
    /// 채굴 장비 프리팹에 부착되는 트리거 컴포넌트.
    ///
    /// 역할:
    ///   MiningTool 레이어 콜라이더(isTrigger=true)가 Junk 레이어 콜라이더를 감지
    ///   → PlayerInteractor.AddNearbyJunk / RemoveNearbyJunk 호출
    ///
    /// 설정:
    ///   - 이 GameObject(또는 부모)의 레이어 = MiningTool
    ///   - 콜라이더 isTrigger = true
    ///   - Physics Layer Matrix: MiningTool ↔ Junk 충돌 활성화
    ///   - UpgradeManager.SpawnMiningEquipment()에서 Init() 주입
    /// </summary>
    public class MiningToolTrigger : MonoBehaviour
    {
        private static int _junkLayer = -1;

        private PlayerInteractor _interactor;

        // ─── 초기화 ──────────────────────────────────────────────────

        /// <summary>장비 스폰 시 PlayerInteractor에서 호출해 참조를 주입한다.</summary>
        public void Init(PlayerInteractor interactor)
        {
            _interactor = interactor;
            
            if (_junkLayer < 0)
                _junkLayer = LayerMask.NameToLayer("Junk");
            
        }

        // ─── Unity Trigger Callbacks ─────────────────────────────────

        private void OnTriggerEnter(Collider other)
        {
            if (_interactor == null)                            return;
            if (other.gameObject.layer != _junkLayer)          return;

            ResourceJunk junk = other.GetComponent<ResourceJunk>();
            if (junk != null)
                _interactor.AddNearbyJunk(junk);
        }

        private void OnTriggerExit(Collider other)
        {
            if (_interactor == null)                            return;
            if (other.gameObject.layer != _junkLayer)          return;

            ResourceJunk junk = other.GetComponent<ResourceJunk>();
            if (junk != null)
                _interactor.RemoveNearbyJunk(junk);
        }
    }
}
