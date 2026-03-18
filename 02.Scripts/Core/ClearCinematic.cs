using System;
using UnityEngine;
using DG.Tweening;
using IdleTycoon.Player;

namespace IdleTycoon.Core
{
    /// <summary>
    /// 게임 클리어 시 카메라 연출.
    ///
    /// 순서:
    ///   1. CameraFollow 비활성화
    ///   2. 플레이어 기준 일정 거리에서 360도 공전
    ///   3. 한 바퀴 완료 → 카메라가 서서히 하늘을 향해 틸트
    ///   4. 동시에 페이드 아웃 (검정 오버레이)
    ///   5. 연출 완료 → onComplete 콜백 (clearPanel 활성화 등)
    ///
    /// 사용법:
    ///   GameManager의 [SerializeField] clearCinematic 에 할당.
    ///   fadeOverlay 에는 전체화면 검정 Image가 포함된 CanvasGroup 을 연결.
    /// </summary>
    public class ClearCinematic : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("비활성화할 팔로우 카메라 컴포넌트")]
        [SerializeField] private CameraFollow cameraFollow;

        [Tooltip("공전 기준점이 될 플레이어 Transform")]
        [SerializeField] private PlayerController playerTarget;
        

        [Tooltip("페이드 아웃용 전체화면 CanvasGroup (검정 Image 포함)")]
        [SerializeField] private CanvasGroup fadeOverlay;

        [Header("Orbit")]
        [Tooltip("플레이어로부터 카메라까지 수평 거리")]
        [SerializeField] private float orbitRadius = 8f;

        [Tooltip("플레이어 기준 카메라 높이 오프셋")]
        [SerializeField] private float orbitHeight = 5f;

        [Tooltip("360도 공전에 걸리는 시간 (초)")]
        [SerializeField] private float orbitDuration = 6f;

        [Header("Outro")]
        [Tooltip("공전 완료 후 카메라가 바라볼 X 각도 (하늘 방향 = 음수)")]
        [SerializeField] private float tiltUpAngle = -85f;

        [Tooltip("틸트 애니메이션 시간 (초)")]
        [SerializeField] private float tiltDuration = 2f;

        [Tooltip("페이드가 시작되는 아웃트로 내 지연 시간 (초)")]
        [SerializeField] private float fadeDelay = 0.4f;

        [Tooltip("페이드 아웃에 걸리는 시간 (초)")]
        [SerializeField] private float fadeDuration = 1.5f;

        // ─── Private ────────────────────────────────────────────────────

        private Camera _cam;

        private void Awake()
        {
            _cam = Camera.main;

            if (fadeOverlay != null)
                fadeOverlay.alpha = 0f;
        }

        // ─── Public API ─────────────────────────────────────────────────

        /// <summary>
        /// 연출을 시작한다. 연출 완료 시 <paramref name="onComplete"/> 호출.
        /// </summary>
        public void Play(Action onComplete)
        {
            if (_cam == null)
                _cam = Camera.main;

            if (cameraFollow != null)
                cameraFollow.enabled = false;

            if (playerTarget == null)
            {
                onComplete?.Invoke();
                return;
            }
            else 
            {
                playerTarget.gameObject.transform.position = Vector3.up;
                playerTarget.enabled = false;
            }

            PlayOrbit(onComplete);
        }

        // ─── Internal Sequence ──────────────────────────────────────────

        private void PlayOrbit(Action onComplete)
        {
            Vector3 pivot = playerTarget.transform.position + Vector3.up * 1f; // 플레이어 중심 높이

            // 현재 카메라 수평 각도로부터 시작
            Vector3 diff = _cam.transform.position - pivot;
            float startAngle = Mathf.Atan2(diff.x, diff.z) * Mathf.Rad2Deg;
            float angle = startAngle;

            DOTween.To(
                () => angle,
                x =>
                {
                    angle = x;
                    Vector3 offset = Quaternion.Euler(0f, angle, 0f) * new Vector3(0f, orbitHeight, -orbitRadius);
                    _cam.transform.position = pivot + offset;
                    _cam.transform.LookAt(pivot);
                },
                startAngle + 360f,
                orbitDuration
            )
            .SetEase(Ease.Linear)
            .OnComplete(() => PlayOutro(onComplete));
        }

        private void PlayOutro(Action onComplete)
        {
            Vector3 endEuler = new Vector3(tiltUpAngle, _cam.transform.eulerAngles.y, 0f);

            Sequence seq = DOTween.Sequence();

            // 카메라 하늘 방향 틸트
            seq.Append(
                _cam.transform.DORotate(endEuler, tiltDuration)
                    .SetEase(Ease.InOutQuad)
            );

            // 페이드 아웃 (일정 시간 뒤 시작)
            if (fadeOverlay != null)
            {
                seq.Insert(fadeDelay,
                    fadeOverlay.DOFade(1f, fadeDuration)
                               .SetEase(Ease.InQuad)
                );
            }

            seq.OnComplete(() => onComplete?.Invoke());
        }
    }
}
