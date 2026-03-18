using System;
using UnityEngine;

namespace IdleTycoon.Upgrades
{
    /// <summary>
    /// 채굴 장비 레벨 데이터 (ScriptableObject).
    ///
    /// 에디터 생성: Create → IdleTycoon → Mining Tool Data
    ///
    /// tools[i] = i번째 업그레이드 장비
    ///   cost            : 업그레이드 비용
    ///   equipPrefab     : 플레이어 ToolHolder 하위에 스폰될 장비 프리팹
    ///                     (MiningTool 레이어 콜라이더 + MiningToolTrigger 부착 필요)
    ///   slotCount       : 동시 채굴 가능한 정크 수 → PlayerInteractor.maxMiningSlots
    ///   speedMultiplier : 채굴속도 배율            → ResourceNode.SetSpeedMultiplier
    ///   stoneCapacity   : Stone 최대 보유량        → StackHolder.UpgradeCapacity
    /// </summary>
    [CreateAssetMenu(fileName = "MiningToolData", menuName = "IdleTycoon/Mining Tool Data")]
    public class MiningToolData : ScriptableObject
    {
        public MiningToolLevel[] tools;
    }

    [Serializable]
    public class MiningToolLevel
    {
        [Tooltip("이 레벨로 업그레이드하는 데 필요한 비용")]
        public int cost;

        [Tooltip("ToolHolder 하위에 스폰될 장비 프리팹 (MiningToolTrigger + MiningTool 레이어 콜라이더 필요)")]
        public GameObject equipPrefab;

        [Tooltip("업그레이드 후 동시 채굴 가능한 정크 수")]
        [Min(1)]
        public int slotCount = 1;

        [Tooltip("채굴 속도 (높을수록 빠름). ResourceJunk 채굴 간격 = mineInterval / mineSpeed")]
        [Min(0.01f)]
        public float mineSpeed = 1f;

        [Tooltip("업그레이드 후 Stone 최대 보유 증가량")]
        [Min(1)]
        public int stoneCapacityDelta = 10;

        [Tooltip("채굴 1회 완료 시 재생할 SFX 클립")]
        public AudioClip mineClip;
    }
}
