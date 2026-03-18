using System;
using IdleTycoon.Items;

namespace IdleTycoon.Core
{
    /// <summary>
    /// 시스템 간 느슨한 결합을 위한 정적 이벤트 허브.
    /// 구독자(UI, Sound, Analytics 등)가 게임플레이 코드를 직접 참조하지 않도록 한다.
    /// </summary>
    public static class EventBus
    {
        // StackHolder 관련
        public static event Action<StackItem> OnItemStacked;
        public static event Action<StackItem> OnItemRemoved;
        public static event Action<int, int> OnStackCountChanged;   // (current, max)

        public static void RaiseItemStacked(StackItem item)         => OnItemStacked?.Invoke(item);
        public static void RaiseItemRemoved(StackItem item)         => OnItemRemoved?.Invoke(item);
        public static void RaiseStackCountChanged(int cur, int max) => OnStackCountChanged?.Invoke(cur, max);

        // 재화 관련
        public static event Action<int> OnMoneyChanged;             // (currentMoney)

        public static void RaiseMoneyChanged(int current)           => OnMoneyChanged?.Invoke(current);

        // 업그레이드 관련
        public static event Action<int, float> OnMineSpeedUpgraded; // (newLevel, newMultiplier)

        public static void RaiseMineSpeedUpgraded(int level, float multiplier)
            => OnMineSpeedUpgraded?.Invoke(level, multiplier);

        // NPC 주문 관련
        public static event Action<int> OnOrderCompleted;              // (rewardAmount)

        public static void RaiseOrderCompleted(int reward)
            => OnOrderCompleted?.Invoke(reward);

        // 게임 진행 관련
        public static event Action<int, int> OnNpcSatisfied;           // (current, target)
        public static event Action           OnGameCleared;

        public static void RaiseNpcSatisfied(int current, int target)
            => OnNpcSatisfied?.Invoke(current, target);

        public static void RaiseGameCleared()
            => OnGameCleared?.Invoke();
    }
}
