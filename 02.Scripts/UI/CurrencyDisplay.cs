using TMPro;
using UnityEngine;
using IdleTycoon.Core;
using IdleTycoon.Economy;

namespace IdleTycoon.UI
{
    /// <summary>
    /// 재화(돈) HUD 표시 컴포넌트.
    ///
    /// EventBus.OnMoneyChanged 를 구독하여 텍스트를 갱신한다.
    /// CurrencyManager 나 다른 게임플레이 코드를 직접 참조하지 않으므로
    /// 느슨한 결합이 유지된다.
    ///
    /// Hierarchy 구조:
    ///   [Canvas]
    ///     └─ [MoneyPanel]   (Anchor: Top-Right)
    ///          ├─ [Icon]    (선택)
    ///          └─ [MoneyText]  ← TMP_Text + CurrencyDisplay.cs
    ///
    /// 설정:
    ///   moneyText  : 이 컴포넌트와 같은 GameObject의 TMP_Text (자동 탐색) 또는 Inspector 지정
    ///   prefix     : 금액 앞에 붙는 문자 (예: "$", "💰", "Gold:")
    ///   useComma   : 천 단위 콤마 표시 여부 (1,234 vs 1234)
    /// </summary>
    public class CurrencyDisplay : MonoBehaviour
    {
        // ─── Inspector ──────────────────────────────────────────────

        [Header("References")]
        [Tooltip("비워두면 같은 GameObject의 TMP_Text를 자동으로 탐색한다.")]
        [SerializeField] private TMP_Text moneyText;

        [Header("Format")]
        [Tooltip("금액 앞에 표시할 접두사")]
        [SerializeField] private string prefix = "$";

        [Tooltip("천 단위 콤마 구분 (1,234 형식)")]
        [SerializeField] private bool useComma = true;

        // ─── Lifecycle ──────────────────────────────────────────────

        private void Awake()
        {
            if (moneyText == null)
                moneyText = GetComponent<TMP_Text>();
        }

        private void Start()
        {
            // 씬 시작 시 현재 재화 값으로 초기화
            if (CurrencyManager.Instance != null)
                UpdateDisplay(CurrencyManager.Instance.Money);
            else
                UpdateDisplay(0);
        }

        private void OnEnable()  => EventBus.OnMoneyChanged += UpdateDisplay;
        private void OnDisable() => EventBus.OnMoneyChanged -= UpdateDisplay;

        // ─── 표시 갱신 ───────────────────────────────────────────────

        private void UpdateDisplay(int money)
        {
            if (moneyText == null) return;
            moneyText.text = useComma
                ? $"{prefix} {money:N0}"
                : $"{prefix} {money}";
        }
    }
}
