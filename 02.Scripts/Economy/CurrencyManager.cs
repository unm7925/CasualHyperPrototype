using UnityEngine;
using IdleTycoon.Core;
using IdleTycoon.Items;
using IdleTycoon.Player;

namespace IdleTycoon.Economy
{
    /// <summary>
    /// 게임 내 재화(돈)를 관리하는 싱글턴.
    ///
    /// 재화 흐름:
    ///   SellZone → AddMoney(value) → _money 증가 + MoneyItem 시각 오브젝트 StackHolder에 추가
    ///   UpgradeZone → SpendMoney(cost) → _money 감소 + MoneyItem 시각 오브젝트 제거
    ///
    /// MoneyItem 시각 오브젝트:
    ///   ItemType.Money StackItem을 Instantiate → StackHolder.Receive 로 전달.
    ///   StackHolder가 슬롯을 관리하고, StackItem이 슬롯으로 Lerp 이동한다.
    ///   moneyItemData가 없거나 StackHolder가 연결되지 않은 경우 시각 표시 없이 숫자만 누적.
    /// </summary>
    public class CurrencyManager : MonoBehaviour
    {
        // ─── Singleton ──────────────────────────────────────────────

        public static CurrencyManager Instance { get; private set; }

        // ─── Inspector ──────────────────────────────────────────────

        [Header("Economy")]
        [SerializeField] private int startingMoney = 0;

        [Header("Money Visual")]
        [Tooltip("ItemType.Money로 설정된 ItemData. 없으면 시각 표시 생략.")]
        [SerializeField] private ItemData moneyItemData;

        [Tooltip("플레이어의 StackHolder. 비워두면 씬에서 자동 탐색.")]
        [SerializeField] private StackHolder stackHolder;

        // ─── State ──────────────────────────────────────────────────

        private int _money;

        public int Money => _money;

        // ─── Lifecycle ──────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            _money   = startingMoney;

            if (stackHolder == null)
                stackHolder = FindObjectOfType<StackHolder>();
        }

        // ─── Public API ─────────────────────────────────────────────

        /// <summary>
        /// 재화를 추가하고 MoneyItem 시각 오브젝트를 StackHolder에 추가한다.
        /// 시각 오브젝트를 호출자가 직접 관리할 때는 AddMoneyRaw()를 사용할 것.
        /// </summary>
        public void AddMoney(int amount)
        {
            if (amount <= 0) return;
            _money += amount;
            EventBus.RaiseMoneyChanged(_money);
            SpawnMoneyVisual();
            AudioManager.Instance?.PlayMoneyGainSFX();
        }

        /// <summary>
        /// 재화만 추가한다 (MoneyItem 시각 오브젝트는 생성하지 않음).
        /// NpcDeliveryZone처럼 시각을 직접 outputPoint에 스폰할 때 사용.
        /// </summary>
        public void AddMoneyRaw(int amount)
        {
            if (amount <= 0) return;
            _money += amount;
            EventBus.RaiseMoneyChanged(_money);
        }

        /// <summary>재화를 소비한다. 성공 시 MoneyItem 1개 제거.</summary>
        public bool SpendMoney(int amount)
        {
            if (amount > _money) return false;
            _money -= amount;
            EventBus.RaiseMoneyChanged(_money);
            AudioManager.Instance?.PlaySpendMoneySFX();
            return true;
        }

        // ─── Money Visual ────────────────────────────────────────────

        private void SpawnMoneyVisual()
        {
            if (stackHolder == null || moneyItemData == null)           return;
            if (moneyItemData.prefab == null)                           return;
            if (!stackHolder.CanReceive(moneyItemData))                 return;

            StackItem item = StackItemPool.Instance != null
                ? StackItemPool.Instance.Get(moneyItemData, stackHolder.transform.position)
                : FallbackInstantiate(moneyItemData, stackHolder.transform.position);

            if (item == null) return;
            stackHolder.Receive(item);
        }

        private StackItem FallbackInstantiate(ItemData data, Vector3 pos)
        {
            GameObject go   = Instantiate(data.prefab, pos, Quaternion.identity);
            StackItem  item = go.GetComponent<StackItem>();
            if (item == null) { Destroy(go); return null; }
            item.Spawn(data, null);
            return item;
        }

        private void DespawnMoneyVisual()
        {
            if (stackHolder == null) return;

            StackItem item = stackHolder.ProvideItemOfType(ItemType.Money);
            item?.Despawn();
        }
    }
}
