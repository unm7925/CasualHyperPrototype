using UnityEngine;
using IdleTycoon.Core;
using IdleTycoon.Economy;
using IdleTycoon.Player;

namespace IdleTycoon.Upgrades
{
    /// <summary>
    /// 업그레이드 종류 열거형.
    /// 새 업그레이드 추가 시 여기에 항목을 추가하고 UpgradeManager switch에 case 추가.
    /// </summary>
    public enum UpgradeType
    {
        MiningTool,
    }

    /// <summary>
    /// 업그레이드 허브 싱글턴.
    ///
    /// MiningTool 업그레이드:
    ///   tools[] 배열을 레벨별로 관리.
    ///   레벨 증가 시 slotCount / speedMultiplier / stoneCapacity 일괄 적용.
    ///
    /// 확장:
    ///   새 업그레이드 → UpgradeType 항목 추가 + CanUpgrade/GetNextCost/TryUpgrade switch에 case 추가.
    /// </summary>
    public class UpgradeManager : MonoBehaviour
    {
        // ─── Singleton ──────────────────────────────────────────────

        public static UpgradeManager Instance { get; private set; }

        // ─── Inspector ──────────────────────────────────────────────

        [Header("Mining Tool Upgrade")]
        [SerializeField] private MiningToolData miningToolData;

        [Header("Scene References")]
        [SerializeField] private PlayerInteractor playerInteractor;
        [SerializeField] private StackHolder      stackHolder;

        // ─── State ──────────────────────────────────────────────────

        private int _miningToolLevel = 1;   // 1 = 미업그레이드

        // ─── Public Properties ───────────────────────────────────────

        public int             MiningToolLevel => _miningToolLevel;

        public bool            CanUpgradeMiningTool =>
            miningToolData != null &&
            miningToolData.tools != null &&
            _miningToolLevel < miningToolData.tools.Length;

        public int             NextMiningToolCost =>
            CanUpgradeMiningTool ? miningToolData.tools[_miningToolLevel].cost : -1;

        /// <summary>현재 장착 중인 툴 레벨 데이터. 미업그레이드 시 null.</summary>
        public MiningToolLevel CurrentTool =>
            miningToolData != null && miningToolData.tools != null && miningToolData.tools.Length>0
            ? miningToolData.tools[_miningToolLevel - 1]
            : null;

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

        // ─── Public API — Generic Routing (UpgradeZone 사용) ────────

        /// <summary>해당 타입의 업그레이드가 가능한지.</summary>
        public bool CanUpgrade(UpgradeType type) => type switch
        {
            UpgradeType.MiningTool => CanUpgradeMiningTool,
            _                      => false
        };

        /// <summary>다음 업그레이드 비용. 불가능하면 -1.</summary>
        public int GetNextCost(UpgradeType type) => type switch
        {
            UpgradeType.MiningTool => NextMiningToolCost,
            _                      => -1
        };

        /// <summary>업그레이드를 실행한다. 성공 시 true.</summary>
        public bool TryUpgrade(UpgradeType type) => type switch
        {
            UpgradeType.MiningTool => TryUpgradeMiningTool(),
            _                      => false
        };

        // ─── Per-Type Upgrade ────────────────────────────────────────

        public bool TryUpgradeMiningTool()
        {
            if (!CanUpgradeMiningTool)
            {
                
                return false;
            }

            if (CurrencyManager.Instance == null) return false;

            _miningToolLevel++;
            MiningToolLevel tool = miningToolData.tools[_miningToolLevel - 1];

            // 동시 채굴 슬롯
            if (playerInteractor != null)
                playerInteractor.maxMiningSlots = tool.slotCount;

            // Stone 최대 용량
            if (stackHolder != null)
                stackHolder.UpgradeCapacity(stackHolder.MaxFrontCapacity, tool.stoneCapacityDelta);

            // 채굴속도는 각 채굴 주체(PlayerInteractor / MinerNPC)가 CurrentTool.mineSpeed를 직접 읽음
            EventBus.RaiseMineSpeedUpgraded(_miningToolLevel, tool.mineSpeed);
            AudioManager.Instance?.PlayUpgradeCompleteSFX();
            
            return true;
        }
    }
}
