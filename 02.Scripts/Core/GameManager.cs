using UnityEngine;

namespace IdleTycoon.Core
{
    /// <summary>
    /// 게임 전체 흐름을 관리하는 싱글턴.
    ///
    /// 클리어 조건:
    ///   EventBus.OnOrderCompleted 수신 횟수(= NPC 만족 수)가
    ///   targetNpcCount에 도달하면 게임 클리어.
    ///
    /// 클리어 처리:
    ///   1. clearPanel 활성화
    ///   2. EventBus.RaiseGameCleared() 발행 (사운드·애널리틱스 등 확장 가능)
    ///   3. Time.timeScale = 0 (선택 — 인스펙터 pauseOnClear로 제어)
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        // ─── Singleton ──────────────────────────────────────────────

        public static GameManager Instance { get; private set; }

        // ─── Inspector ──────────────────────────────────────────────

        [Header("Clear Condition")]
        [Tooltip("게임 클리어에 필요한 NPC 만족(주문 완료) 수")]
        [SerializeField] private int targetNpcCount = 20;

        [Header("UI")]
        [Tooltip("클리어 시 활성화할 UI 패널")]
        [SerializeField] private GameObject clearPanel;

        [Header("Cinematic")]
        [Tooltip("클리어 카메라 연출 컴포넌트 (없으면 즉시 패널 표시)")]
        [SerializeField] private ClearCinematic clearCinematic;

        [Header("Options")]
        [Tooltip("클리어 시 Time.timeScale을 0으로 설정하여 게임을 일시정지")]
        [SerializeField] private bool pauseOnClear = true;

        // ─── State ──────────────────────────────────────────────────

        private int  _satisfiedCount;
        private bool _isCleared;

        public int  SatisfiedCount => _satisfiedCount;
        public int  TargetNpcCount => targetNpcCount;
        public bool IsCleared      => _isCleared;

        // ─── Lifecycle ──────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (clearPanel != null)
                clearPanel.SetActive(false);
        }

        private void OnEnable()  => EventBus.OnOrderCompleted += HandleOrderCompleted;
        private void OnDisable() => EventBus.OnOrderCompleted -= HandleOrderCompleted;

        // ─── EventBus 핸들러 ─────────────────────────────────────────

        private void HandleOrderCompleted(int reward)
        {
            if (_isCleared) return;

            _satisfiedCount++;
            EventBus.RaiseNpcSatisfied(_satisfiedCount, targetNpcCount);

            if (_satisfiedCount >= targetNpcCount)
                TriggerClear();
        }

        // ─── 클리어 처리 ─────────────────────────────────────────────

        private void TriggerClear()
        {
            _isCleared = true;

            EventBus.RaiseGameCleared();

            if (clearCinematic != null)
                clearCinematic.Play(OnCinematicComplete);
            else
                OnCinematicComplete();
        }

        private void OnCinematicComplete()
        {
            if (clearPanel != null)
                clearPanel.SetActive(true);

            if (pauseOnClear)
                Time.timeScale = 0f;
        }
    }
}
