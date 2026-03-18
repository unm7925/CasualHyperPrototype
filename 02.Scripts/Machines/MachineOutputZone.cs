using UnityEngine;
using IdleTycoon.Core;
using IdleTycoon.Items;
using IdleTycoon.Player;

namespace IdleTycoon.Machines
{
    /// <summary>
    /// 머신 아웃풋 존 — 출력 존 (IInteractable).
    /// ProcessingMachine의 OutputBuffer에 쌓인 아이템을
    /// 플레이어 StackHolder로 transferInterval마다 1개씩 전달한다.
    ///
    /// 세팅:
    ///   ProcessingMachine 자식 GameObject에 붙이고
    ///   machine 필드에 부모 ProcessingMachine을 연결.
    ///   별도의 Trigger Collider 필요 (BoxCollider 등).
    /// </summary>
    public class MachineOutputZone : MonoBehaviour, IInteractable
    {
        // ─── Inspector ──────────────────────────────────────────────

        [Header("References")]
        [SerializeField] private ProcessingMachine machine;

        [Header("Timing")]
        [Tooltip("플레이어에게 아이템을 전달하는 간격 (초)")]
        [SerializeField] private float transferInterval = 0.3f;

        // ─── Public ─────────────────────────────────────────────────

        /// <summary>DeliveryNPC가 HasOutput / TakeOutput에 접근하기 위해 사용.</summary>
        public ProcessingMachine Machine => machine;

        // ─── State ──────────────────────────────────────────────────

        private PlayerInteractor _interactor;
        private bool             _playerInZone;
        private float            _transferTimer;

        // ─── Lifecycle ──────────────────────────────────────────────

        private void Awake()
        {
            if (machine == null)
                machine = GetComponentInParent<ProcessingMachine>();
        }

        private void Update()
        {
            HandleOutputTransfer();
        }

        // ─── IInteractable ──────────────────────────────────────────

        public void OnPlayerEnter(Component player)
        {
            if (player is not PlayerInteractor interactor) return;
            _interactor    = interactor;
            _playerInZone  = true;
            _transferTimer = transferInterval; // 진입 즉시 첫 전달
        }

        public void OnPlayerStay(Component player) { }  // Update에서 처리

        public void OnPlayerExit(Component player)
        {
            _playerInZone = false;
            _interactor   = null;
            _transferTimer = 0f;
        }

        // ─── 아웃풋 전달 ─────────────────────────────────────────────

        private void HandleOutputTransfer()
        {
            if (!_playerInZone || _interactor == null) return;
            if (!machine.HasOutput)                    return;
            if (!_interactor.StackHolder.CanReceive(machine.Data.outputItem)) return;

            _transferTimer += Time.deltaTime;
            if (_transferTimer < transferInterval) return;

            _transferTimer = 0f;

            StackItem item = machine.TakeOutput();
            if (item == null) return;

            _interactor.StackHolder.Receive(item);
        }

        // ─── Gizmo ──────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.1f, new Vector3(1.5f, 0.2f, 1.5f));
        }
#endif
    }
}
