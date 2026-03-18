using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using IdleTycoon.Core;
using IdleTycoon.Items;
using IdleTycoon.Player;

namespace IdleTycoon.Machines
{
    /// <summary>
    /// 가공 머신 — 입력 존 (IInteractable).
    ///
    /// 흐름:
    ///   1. 플레이어가 입력 존 트리거에 진입
    ///   2. StackHolder에 inputItemType이 있으면 transferInterval마다 1개 수거
    ///   3. 수거된 아이템은 Despawn 없이 inputOrigin 슬롯으로 Lerp 이동 (시각 유지)
    ///   4. processTime 경과 후 inputOrigin 맨 앞 아이템 Despawn + outputItem 스폰 동시 처리
    ///   5. MachineOutputZone이 OutputBuffer를 플레이어에게 전달
    ///
    /// Input 슬롯 구조:
    ///   StackHolder와 동일하게 Transform 슬롯을 사전 생성한다.
    ///   수거된 StackItem은 AssignSlot()을 통해 해당 슬롯으로 Lerp 이동한다.
    ///   처리 순서는 FIFO (먼저 들어온 것부터 처리).
    ///
    /// Hierarchy:
    ///   [ProcessingMachine]  ← 이 컴포넌트 + 입력 트리거 Collider
    ///     ├─ [InputOrigin]   ← 인풋 아이템이 쌓일 기준 위치
    ///     ├─ [OutputOrigin]  ← 아웃풋 아이템이 쌓일 기준 위치
    ///     └─ [OutputZone]   ← MachineOutputZone + 출력 트리거 Collider
    /// </summary>
    public class ProcessingMachine : MonoBehaviour, IInteractable
    {
        // ─── Inspector ──────────────────────────────────────────────

        [Header("Data")]
        [SerializeField] private MachineData machineData;

        [Header("Input Slots")]
        [Tooltip("수거된 아이템이 Lerp로 이동해 쌓일 기준 위치")]
        [SerializeField] private Transform inputOrigin;
        [SerializeField] private Vector3   inputSlotOffset  = new Vector3(0f, 0.25f, 0f);
        [SerializeField] private int       maxInputCapacity = 10;

        [Header("Output Slots")]
        [Tooltip("아웃풋 아이템이 시각적으로 쌓일 기준 위치")]
        [SerializeField] private Transform outputOrigin;
        [SerializeField] private Vector3   outputSlotOffset = new Vector3(0f, 0.25f, 0f);

        // ─── State ──────────────────────────────────────────────────

        private PlayerInteractor         _interactor;
        private bool                     _playerInInputZone;
        private float                    _transferTimer;

        private readonly List<StackItem>  _inputBuffer  = new();   // inputOrigin에 쌓인 아이템
        private readonly List<Transform>  _inputSlots   = new();   // inputOrigin 슬롯 Transforms
        private readonly List<StackItem>  _outputBuffer = new();   // 처리 완료 아이템
        private bool                     _isProcessing;

        // ─── Public ─────────────────────────────────────────────────

        public bool        HasOutput   => _outputBuffer.Count > 0;
        public int         OutputCount => _outputBuffer.Count;
        public MachineData Data        => machineData;

        /// <summary>
        /// MinerNPC가 채굴한 StackItem을 직접 전달한다.
        /// inputOrigin 슬롯으로 AssignSlot → Lerp 이동 → ProcessQueue 자동 시작.
        /// </summary>
        public bool ReceiveItem(StackItem item)
        {
            if (item == null)                          return false;
            if (_inputBuffer.Count >= maxInputCapacity) return false;

            _inputBuffer.Add(item);

            if (_inputSlots.Count >= _inputBuffer.Count) 
            {
                item.AssignSlot(_inputSlots[_inputBuffer.Count - 1]);
                
                item.transform.localScale = Vector3.zero;
                item.transform.DOScale(Vector3.one * 2f,0.15f).SetEase(Ease.OutQuad)
                    .OnComplete(()=>item.transform.DOScale(Vector3.one,0.1f));
            }

            if (!_isProcessing)
                StartCoroutine(ProcessQueue());

            return true;
        }

        /// <summary>MachineOutputZone이 호출해서 출력 아이템을 가져감.</summary>
        public StackItem TakeOutput()
        {
            if (_outputBuffer.Count == 0) return null;
            int last = _outputBuffer.Count - 1;
            StackItem item = _outputBuffer[last];
            _outputBuffer.RemoveAt(last);
            item.AssignSlot(null);
            return item;
        }

        // ─── Lifecycle ──────────────────────────────────────────────

        private void Awake()
        {
            BuildInputSlots();
        }

        // ─── IInteractable ──────────────────────────────────────────

        public void OnPlayerEnter(Component player)
        {
            if (player is not PlayerInteractor interactor) return;
            _interactor        = interactor;
            _playerInInputZone = true;
            _transferTimer     = machineData.transferInterval; // 진입 즉시 첫 수거
        }

        public void OnPlayerStay(Component player) { }

        public void OnPlayerExit(Component player)
        {
            _playerInInputZone = false;
            _interactor        = null;
            _transferTimer     = 0f;
        }

        // ─── Update ─────────────────────────────────────────────────

        private void Update()
        {
            HandleInputTransfer();
        }

        private void HandleInputTransfer()
        {
            if (!_playerInInputZone || _interactor == null)                          return;
            if (!_interactor.StackHolder.HasItemOfType(machineData.inputItemType))   return;
            if (_inputBuffer.Count >= maxInputCapacity)                              return;

            _transferTimer += Time.deltaTime;
            if (_transferTimer < machineData.transferInterval) return;

            _transferTimer = 0f;
            TakeItemFromPlayer();
        }

        // ─── Input 수거 — inputOrigin 슬롯으로 Lerp 이동 ────────────

        private void TakeItemFromPlayer()
        {
            StackItem item = _interactor.StackHolder.ProvideItemOfType(machineData.inputItemType);
            if (item == null) return;
            
            AudioManager.Instance.PlayGiveItemSFX();
            _inputBuffer.Add(item);
            
            item.transform.localScale = Vector3.zero;
            item.transform.DOScale(Vector3.one * 2f,0.15f).SetEase(Ease.OutQuad)
                .OnComplete(()=>item.transform.DOScale(Vector3.one,0.1f));
            
            // 슬롯 할당 → StackItem이 inputOrigin 슬롯 위치로 Lerp 이동
            if (_inputSlots.Count >= _inputBuffer.Count)
                item.AssignSlot(_inputSlots[_inputBuffer.Count - 1]);

            if (!_isProcessing)
                StartCoroutine(ProcessQueue());
        }

        // ─── 변환 처리 ──────────────────────────────────────────────

        private IEnumerator ProcessQueue()
        {
            _isProcessing = true;

            while (_inputBuffer.Count > 0)
            {
                yield return new WaitForSeconds(machineData.processTime);

                // inputOrigin 맨 앞 아이템 Despawn + 아웃풋 아이템 스폰 동시 처리
                ConsumeInputItem();
                ProduceOutput();
            }

            _isProcessing = false;
        }

        /// <summary>inputBuffer 맨 앞(FIFO) 아이템을 슬롯에서 제거하고 Despawn.</summary>
        private void ConsumeInputItem()
        {
            if (_inputBuffer.Count == 0) return;

            StackItem item = _inputBuffer[0];
            _inputBuffer.RemoveAt(0);
            item.AssignSlot(null);

            // 나머지 아이템 슬롯 인덱스 한 칸 앞으로 재배정 → Lerp로 자연스럽게 이동
            for (int i = 0; i < _inputBuffer.Count; i++)
                _inputBuffer[i].AssignSlot(_inputSlots[i]);

            item.Despawn();
        }

        private void ProduceOutput()
        {
            if (_outputBuffer.Count >= machineData.maxOutputCapacity) return;

            ItemData data = machineData.outputItem;
            if (data == null || data.prefab == null)
            {
                
                return;
            }

            Vector3 spawnPos = outputOrigin != null
                ? outputOrigin.position + outputSlotOffset * _outputBuffer.Count
                : transform.position    + outputSlotOffset * _outputBuffer.Count;

            StackItem item = StackItemPool.Instance != null
                ? StackItemPool.Instance.Get(data, spawnPos)
                : FallbackInstantiate(data, spawnPos);

            if (item == null) return;
            _outputBuffer.Add(item);

            AudioManager.Instance?.PlayMachineSFX();
        }

        // ─── Input 슬롯 생성 ─────────────────────────────────────────

        /// <summary>
        /// inputOrigin 기준으로 maxInputCapacity개의 Transform 슬롯을 사전 생성.
        /// StackHolder.RebuildSlots()과 동일한 패턴.
        /// </summary>
        private void BuildInputSlots()
        {
            if (inputOrigin == null) return;

            for (int i = 0; i < maxInputCapacity; i++)
            {
                var slotGo = new GameObject($"InputSlot_{i}");
                slotGo.transform.SetParent(inputOrigin);
                slotGo.transform.localPosition = inputSlotOffset * i;
                slotGo.transform.localRotation = Quaternion.identity;
                _inputSlots.Add(slotGo.transform);
            }
        }

        private StackItem FallbackInstantiate(ItemData data, Vector3 pos)
        {
            GameObject go   = Instantiate(data.prefab, pos, Quaternion.identity);
            StackItem  item = go.GetComponent<StackItem>();
            if (item == null) { Destroy(go); return null; }
            item.Spawn(data, null);
            return item;
        }

        // ─── Gizmo ──────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = _isProcessing ? Color.green : Color.grey;
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.5f, new Vector3(1f, 1f, 1f));

            if (inputOrigin != null)
            {
                Gizmos.color = Color.yellow;
                for (int i = 0; i < maxInputCapacity; i++)
                    Gizmos.DrawWireSphere(inputOrigin.position + inputSlotOffset * i, 0.08f);
            }
        }
#endif
    }
}
