using TMPro;
using UnityEngine;
using IdleTycoon.Core;

namespace IdleTycoon.UI
{
    /// <summary>
    /// NPC 만족 진행도 표시 컴포넌트.
    ///
    /// EventBus.OnNpcSatisfied 를 구독하여 "N / 20" 형식으로 갱신한다.
    ///
    /// Hierarchy 예시:
    ///   [Canvas]
    ///     └─ [ProgressPanel]  (Anchor: Top-Left 등)
    ///          └─ [ProgressText]  ← TMP_Text + NpcProgressDisplay.cs
    /// </summary>
    public class NpcProgressDisplay : MonoBehaviour
    {
        // ─── Inspector ──────────────────────────────────────────────

        [Tooltip("비워두면 같은 GameObject의 TMP_Text를 자동 탐색한다.")]
        [SerializeField] private TMP_Text progressText;

        [Tooltip("표시 형식. {0} = 현재, {1} = 목표")]
        [SerializeField] private string format = "NPC {0} / {1}";

        // ─── Lifecycle ──────────────────────────────────────────────

        private void Awake()
        {
            if (progressText == null)
                progressText = GetComponent<TMP_Text>();
        }

        private void Start()
        {
            if (GameManager.Instance != null)
                UpdateDisplay(GameManager.Instance.SatisfiedCount, GameManager.Instance.TargetNpcCount);
            else
                UpdateDisplay(0, 0);
        }

        private void OnEnable()  => EventBus.OnNpcSatisfied += UpdateDisplay;
        private void OnDisable() => EventBus.OnNpcSatisfied -= UpdateDisplay;

        // ─── 표시 갱신 ───────────────────────────────────────────────

        private void UpdateDisplay(int current, int target)
        {
            if (progressText == null) return;
            progressText.text = string.Format(format, current, target);
        }
    }
}
