using IdleTycoon.Core;
using UnityEngine;
using UnityEngine.UI;

namespace IdleTycoon.UI
{
    /// <summary>
    /// 사운드 ON/OFF 토글 버튼.
    /// AudioListener.pause 로 전체 오디오를 제어한다.
    /// </summary>
    public class SoundButton : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Button button;

        [Header("Icons")]
        [Tooltip("사운드 ON 상태일 때 표시할 이미지")]
        [SerializeField] private GameObject iconOn;
        [Tooltip("사운드 OFF 상태일 때 표시할 이미지")]
        [SerializeField] private GameObject iconOff;

        // ─── Lifecycle ──────────────────────────────────────────────

        private void Awake()
        {
            if (button == null)
                button = GetComponent<Button>();
            
            button.onClick.AddListener(Toggle);
        }

        private void Start()
        {
            RefreshIcons();
        }

        private void OnDestroy()
        {
            button.onClick.RemoveListener(Toggle);
        }

        // ─── Toggle ─────────────────────────────────────────────────

        private void Toggle()
        {
            AudioListener.pause = !AudioListener.pause;
            RefreshIcons();
        }

        private void RefreshIcons()
        {
            bool isOn = !AudioListener.pause;
            if (iconOn  != null) iconOn.SetActive(isOn);
            if (iconOff != null) iconOff.SetActive(!isOn);
        }
    }
}
