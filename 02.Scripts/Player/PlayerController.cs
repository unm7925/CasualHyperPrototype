using UnityEngine;

namespace IdleTycoon.Player
{
    /// <summary>
    /// 이동 전담 컴포넌트.
    /// - PC  : WASD / Arrow Keys
    /// - Mobile : 화면 터치 드래그 (가상 조이스틱 없이 직접 델타 계산)
    /// CharacterController로 이동, 회전은 이동 방향을 향해 Slerp.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        // ─── Inspector ──────────────────────────────────────────────

        [Header("Movement")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float rotationSpeed = 12f;
        [SerializeField] private float gravity = -20f;

        [Header("Mobile Touch")]
        [Tooltip("터치 드래그 감도 (픽셀 → 방향 벡터 스케일)")]
        [SerializeField] private float touchSensitivity = 0.005f;

        // ─── Components ─────────────────────────────────────────────

        private CharacterController _cc;
        private Animator _animator;          // 없으면 무시

        // ─── State ──────────────────────────────────────────────────

        private Vector3 _velocity;           // 중력 누적용
        private Vector2 _inputDir;           // 정규화된 2D 입력 방향

        // 터치 추적
        private int   _touchId  = -1;
        private Vector2 _touchStartPos;

        // 애니메이터 파라미터 캐싱
        private static readonly int AnimSpeed = Animator.StringToHash("Speed");
        

        // ─── Lifecycle ──────────────────────────────────────────────

        private void Awake()
        {
            _cc       = GetComponent<CharacterController>();
            _animator = GetComponent<Animator>(); // optional
        }

        private void Update()
        {
            ReadInput();
            Move();
        }

        // ─── Input ──────────────────────────────────────────────────

        private void ReadInput()
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            // WASD / Arrow Keys
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            _inputDir = new Vector2(h, v).normalized;
#else
            ReadTouchInput();
#endif
        }

        private void ReadTouchInput()
        {
            if (Input.touchCount == 0)
            {
                _touchId   = -1;
                _inputDir  = Vector2.zero;
                return;
            }

            foreach (Touch touch in Input.touches)
            {
                switch (touch.phase)
                {
                    case TouchPhase.Began:
                        if (_touchId == -1)
                        {
                            _touchId       = touch.fingerId;
                            _touchStartPos = touch.position;
                        }
                        break;

                    case TouchPhase.Moved:
                    case TouchPhase.Stationary:
                        if (touch.fingerId == _touchId)
                        {
                            Vector2 delta = (touch.position - _touchStartPos) * touchSensitivity;
                            _inputDir = Vector2.ClampMagnitude(delta, 1f);
                        }
                        break;

                    case TouchPhase.Ended:
                    case TouchPhase.Canceled:
                        if (touch.fingerId == _touchId)
                        {
                            _touchId  = -1;
                            _inputDir = Vector2.zero;
                        }
                        break;
                }
            }
        }

        // ─── Movement ───────────────────────────────────────────────

        private void Move()
        {
            
            // 수평 이동 벡터 (카메라 기준 없이 월드 XZ)
            Vector3 moveDir = new Vector3(_inputDir.x, 0f, _inputDir.y);

            // 중력
            if (_cc.isGrounded)
                _velocity.y = -2f;          // 바닥에 붙어있게
            else
                _velocity.y += gravity * Time.deltaTime;

            Vector3 motion = moveDir * moveSpeed + Vector3.up * _velocity.y;
            
            _cc.Move(motion * Time.deltaTime);
            

            // 회전
            if (moveDir.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(moveDir, Vector3.up);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
            }

            // 애니메이터
            if (_animator != null)
                _animator.SetFloat(AnimSpeed, moveDir.magnitude);
        }

        // ─── Public API ─────────────────────────────────────────────

        /// <summary>외부(컷씬 등)에서 이동을 잠글 때 사용.</summary>
        public void SetMovementLocked(bool locked)
        {
            enabled = !locked;
            if (locked) _inputDir = Vector2.zero;
        }
    }
}
