using System;
using System.Collections;
using UnityEngine;
using IdleTycoon.Items;
using IdleTycoon.Player;

namespace IdleTycoon.Nodes
{
    /// <summary>
    /// 채굴 가능한 개별 오브젝트.
    ///
    /// 두 가지 채굴 주체:
    ///   1. PlayerInteractor (플레이어)
    ///      StartMining(PlayerInteractor, Action) → StackHolder.Receive
    ///   2. MonoBehaviour 채굴자 (MinerNPC 등)
    ///      StartMining(MonoBehaviour, Action<StackItem>, Action) → StackItem 콜백으로 전달
    ///
    /// 클레임 시스템:
    ///   TryClaim() — 이동 전 선점, 다른 주체가 중복 채굴하지 않도록 방지
    ///   Unclaim()  — 이동 중단 또는 채굴 포기 시 해제
    ///   StartMining / StopMining / Respawn에서 자동 해제
    ///
    /// 주의: SetActive(false) 이후 코루틴이 강제 정지되므로
    ///       모든 콜백은 반드시 SetActive 호출 전에 처리한다.
    /// </summary>
    public class ResourceJunk : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private NodeData nodeData;

        // ─── State ──────────────────────────────────────────────────

        private float            _mineSpeed = 1f;   // StartMining 호출 시 주입, 코루틴에서 사용
        private Coroutine        _miningCoroutine;
        private PlayerInteractor _interactor;
        private Action<ResourceJunk> _onMined;

        private bool          _isAvailable = true;
        private MonoBehaviour _claimedBy;              // 현재 채굴권을 가진 주체

        // ─── Public Properties ───────────────────────────────────────

        public bool     IsAvailable => _isAvailable;
        public bool     IsClaimed   => _claimedBy != null;
        public ItemData OutputItem  => nodeData != null ? nodeData.outputItem : null;

        public bool IsClaimedBy(MonoBehaviour agent) => ReferenceEquals(_claimedBy, agent);

        /// <summary>채굴 완료 시 발행 — ResourceNode가 구독해서 리스폰 시작.</summary>
        public event Action<ResourceJunk> Mined;

        private float EffectiveMineInterval => nodeData.mineInterval / Mathf.Max(0.01f, _mineSpeed);

        // ─── 클레임 API ───────────────────────────────────────────────

        /// <summary>
        /// 채굴권 선점. 다른 주체가 이미 점유 중이면 false.
        /// MinerNPC가 이동 시작 전에 호출하여 중복 타겟 방지.
        /// </summary>
        public bool TryClaim(MonoBehaviour claimer)
        {
            if (!_isAvailable) return false;
            if (_claimedBy != null && !ReferenceEquals(_claimedBy, claimer)) return false;
            _claimedBy = claimer;
            return true;
        }

        /// <summary>채굴 포기 시 클레임 해제. StartMining/StopMining/Respawn에서 자동 호출.</summary>
        public void Unclaim(MonoBehaviour claimer)
        {
            if (ReferenceEquals(_claimedBy, claimer))
                _claimedBy = null;
        }

        // ─── PlayerInteractor API ────────────────────────────────────

        /// <summary>
        /// 플레이어 채굴 시작. 다른 주체가 점유 중이면 false.
        /// mineInterval / mineSpeed 간격마다 StackItem 생성 → StackHolder.Receive → 비활성화.
        /// </summary>
        public bool StartMining(PlayerInteractor interactor, Action<ResourceJunk> onMined,
                                float mineSpeed = 1f)
        {
            if (!_isAvailable) return false;
            if (_claimedBy != null && !ReferenceEquals(_claimedBy, interactor)) return false;

            _claimedBy  = interactor;
            _interactor = interactor;
            _onMined    = onMined;
            _mineSpeed  = Mathf.Max(0.01f, mineSpeed);

            if (_miningCoroutine != null) StopCoroutine(_miningCoroutine);
            _miningCoroutine = StartCoroutine(MiningCoroutine());
            return true;
        }

        // ─── NPC API ─────────────────────────────────────────────────

        /// <summary>
        /// NPC 채굴 시작. StackHolder 없이 StackItem을 콜백으로 반환.
        /// 채굴 완료 시 onItemProduced(item) → NPC가 머신으로 직접 전달.
        /// </summary>
        public bool StartMining(MonoBehaviour claimer,
                                Action<StackItem>    onItemProduced,
                                Action<ResourceJunk> onMined,
                                float mineSpeed = 1f)
        {
            if (!_isAvailable) return false;
            if (_claimedBy != null && !ReferenceEquals(_claimedBy, claimer)) return false;

            _claimedBy = claimer;
            _mineSpeed = Mathf.Max(0.01f, mineSpeed);

            if (_miningCoroutine != null) StopCoroutine(_miningCoroutine);
            _miningCoroutine = StartCoroutine(MiningCoroutineNPC(onItemProduced, onMined));
            return true;
        }

        /// <summary>채굴 중단. 클레임 해제.</summary>
        public void StopMining()
        {
            if (_miningCoroutine != null)
            {
                StopCoroutine(_miningCoroutine);
                _miningCoroutine = null;
            }
            _claimedBy  = null;
            _interactor = null;
            _onMined    = null;
        }

        // ─── 코루틴 — 플레이어 ───────────────────────────────────────

        private IEnumerator MiningCoroutine()
        {
            while (_isAvailable && _interactor != null)
            {
                yield return new WaitForSeconds(EffectiveMineInterval);

                if (!_isAvailable || _interactor == null) break;

                bool canReceive = _interactor.StackHolder.CanReceive(nodeData.outputItem);
                if (canReceive)
                    DeliverToStackHolder();

                // SetActive(false) 전에 모든 콜백 처리
                _isAvailable = false;
                _claimedBy   = null;

                Action<ResourceJunk> notifyPlayer = _onMined;
                _onMined    = null;
                _interactor = null;

                Mined?.Invoke(this);
                notifyPlayer?.Invoke(this);

                _miningCoroutine = null;
                gameObject.SetActive(false);
                yield break;
            }

            _miningCoroutine = null;
        }

        // ─── 코루틴 — NPC ────────────────────────────────────────────

        private IEnumerator MiningCoroutineNPC(Action<StackItem>    onItemProduced,
                                               Action<ResourceJunk> onMined)
        {
            while (_isAvailable && _claimedBy != null)
            {
                yield return new WaitForSeconds(EffectiveMineInterval);

                if (!_isAvailable || _claimedBy == null) break;

                // StackItem 생성 (슬롯 없음 — NPC가 머신 슬롯으로 AssignSlot)
                StackItem item = CreateItem();

                // SetActive(false) 전에 모든 콜백 처리
                _isAvailable = false;
                _claimedBy   = null;

                Action<ResourceJunk> notify = onMined;

                onItemProduced?.Invoke(item);   // NPC → ProcessingMachine.ReceiveItem
                Mined?.Invoke(this);            // ResourceNode 리스폰 트리거
                notify?.Invoke(this);           // NPC 다음 정크 탐색

                _miningCoroutine = null;
                gameObject.SetActive(false);
                yield break;
            }

            _miningCoroutine = null;
        }

        // ─── 아이템 생성 헬퍼 ────────────────────────────────────────

        /// <summary>StackItem을 정크 위치에 생성 (슬롯 미배정).</summary>
        private StackItem CreateItem()
        {
            ItemData data = nodeData.outputItem;
            if (data == null || data.prefab == null)
            {
                
                return null;
            }
            
            return StackItemPool.Instance != null
                ? StackItemPool.Instance.Get(data, transform.position)
                : FallbackInstantiate(data);
        }

        private StackItem FallbackInstantiate(ItemData data)
        {
            GameObject go   = Instantiate(data.prefab, transform.position, Quaternion.identity);
            StackItem  item = go.GetComponent<StackItem>();
            if (item == null) { Destroy(go); return null; }
            item.Spawn(data, null);
            return item;
        }

        private void DeliverToStackHolder()
        {
            StackItem item = CreateItem();
            if (item != null) _interactor.StackHolder.Receive(item);
        }

        // ─── 리스폰 ──────────────────────────────────────────────────

        /// <summary>ResourceNode가 리스폰 타이머 완료 후 호출.</summary>
        public void Respawn()
        {
            _isAvailable = true;
            _claimedBy   = null;
            gameObject.SetActive(true);
        }

        // ─── Gizmo ──────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = !_isAvailable ? Color.red
                         : IsClaimed      ? Color.yellow
                                          : Color.green;
            Gizmos.DrawWireCube(transform.position, Vector3.one * 0.4f);
        }
#endif
    }
}
