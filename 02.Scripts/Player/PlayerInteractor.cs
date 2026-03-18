using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using IdleTycoon.Core;
using IdleTycoon.Nodes;
using IdleTycoon.Upgrades;

namespace IdleTycoon.Player
{
    /// <summary>
    /// 상호작용 전담 컴포넌트.
    ///
    /// 두 가지 역할:
    ///   1. IInteractable 존 (SellZone, UpgradeZone 등) — 기존 _current 단일 처리
    ///   2. 채굴 관리 — MiningToolTrigger에서 AddNearbyJunk/RemoveNearbyJunk 수신
    ///
    /// 채굴 장비 흐름:
    ///   ResourceNode.OnTriggerEnter → SpawnMiningEquipment()
    ///     → UpgradeManager.CurrentTool.equipPrefab 인스턴스화 → ToolHolder 하위
    ///     → MiningToolTrigger.Init(this) 주입
    ///   ResourceNode.OnTriggerExit  → DespawnMiningEquipment()
    ///     → 장비 Destroy + 채굴 전부 중단
    ///
    /// 채굴 슬롯 관리:
    ///   MiningToolTrigger → AddNearbyJunk / RemoveNearbyJunk
    ///   → RefreshMiningSlots(): 거리 순 정렬, maxMiningSlots개 StartMining
    ///
    /// Hierarchy 구조:
    ///   [Player]
    ///     ├─ SphereCollider (isTrigger=true)  ← IInteractable 존 감지
    ///     ├─ PlayerInteractor
    ///     ├─ StackHolder
    ///     └─ [ToolHolder]                     ← 장비 프리팹 스폰 위치
    /// </summary>
    public class PlayerInteractor : MonoBehaviour
    {
        // ─── Inspector ──────────────────────────────────────────────

        [Header("References")]
        [SerializeField] private StackHolder stackHolder;

        [Header("Interaction")]
        [Tooltip("OnPlayerStay 콜백 최소 간격 (초). 0이면 매 프레임 호출.")]
        [SerializeField] private float stayInterval = 0.1f;

        [Header("Mining")]
        [Tooltip("장비 프리팹이 스폰될 빈 Transform (Player 하위 ToolHolder)")]
        [SerializeField] private Transform toolHolder;

        [Tooltip("동시 채굴 가능한 정크 수. UpgradeManager가 업그레이드 시 갱신.")]
        public int maxMiningSlots = 1;

        // ─── State — IInteractable ───────────────────────────────────

        private IInteractable _current;
        
        private float         _stayTimer;
        
        private Animator _animator;
        private static readonly int AnimInteract = Animator.StringToHash("Interact");

        // ─── State — Mining ──────────────────────────────────────────

        private readonly List<ResourceJunk>    _nearbyJunks  = new();
        private readonly HashSet<ResourceJunk> _miningJunks  = new();
        private GameObject                     _currentEquipment;

        // ─── Public Properties ──────────────────────────────────────

        public StackHolder StackHolder => stackHolder;

        // ─── Lifecycle ──────────────────────────────────────────────

        private void Awake()
        {
            _animator =  GetComponent<Animator>();
            
            if (stackHolder == null)
                stackHolder = GetComponent<StackHolder>();
        }

        private void Update()
        {
            if (_current == null) return;
            
            _stayTimer += Time.deltaTime;
            if (_stayTimer >= stayInterval)
            {
                _stayTimer = 0f;
                _current.OnPlayerStay(this);
            }
        }

        // ─── Unity Trigger Callbacks — IInteractable 존 감지 ────────

        private void OnTriggerEnter(Collider other)
        {
            IInteractable interactable = other.GetComponentInParent<IInteractable>();
            if (interactable == null) return;

            if (_animator != null) 
            {
                _animator?.SetTrigger(AnimInteract);
            }
            if (_current != null && !ReferenceEquals(_current, interactable))
                _current.OnPlayerExit(this);

            _current   = interactable;
            _stayTimer = stayInterval; // 진입 즉시 첫 Stay 실행
            _current.OnPlayerEnter(this);
        }

        private void OnTriggerExit(Collider other)
        {
            IInteractable interactable = other.GetComponentInParent<IInteractable>();
            if (interactable == null || !ReferenceEquals(_current, interactable)) return;

            _current.OnPlayerExit(this);
            _current   = null;
            _stayTimer = 0f;
        }

        // ─── 채굴 장비 스폰/디스폰 (ResourceNode 호출) ──────────────

        /// <summary>
        /// ResourceNode 진입 시 호출.
        /// UpgradeManager.CurrentTool의 equipPrefab을 ToolHolder 하위에 스폰한다.
        /// </summary>
        public void SpawnMiningEquipment()
        {
            if (_currentEquipment != null) return; // 이미 장착 중

            if (UpgradeManager.Instance == null) return;

            MiningToolLevel tool = UpgradeManager.Instance.CurrentTool;
            if (tool == null || tool.equipPrefab == null) return;

            Transform parent  = toolHolder != null ? toolHolder : transform;
            _currentEquipment = Instantiate(tool.equipPrefab, parent);
            _currentEquipment.transform.localPosition = Vector3.zero;
            _currentEquipment.transform.localRotation = Quaternion.identity;

            // MiningToolTrigger에 PlayerInteractor 주입
            MiningToolTrigger trigger = _currentEquipment.GetComponent<MiningToolTrigger>();
            trigger?.Init(this);
        }

        /// <summary>
        /// ResourceNode 이탈 시 호출.
        /// 장비를 제거하고 모든 채굴을 중단한다.
        /// </summary>
        public void DespawnMiningEquipment()
        {
            // 채굴 중단
            foreach (ResourceJunk j in _miningJunks)
                j.StopMining();
            _miningJunks.Clear();
            _nearbyJunks.Clear();

            // 장비 제거
            if (_currentEquipment != null)
            {
                Destroy(_currentEquipment);
                _currentEquipment = null;
            }
        }

        // ─── Nearby Junk API (MiningToolTrigger 호출) ────────────────

        /// <summary>MiningToolTrigger가 Junk 레이어 감지 시 호출.</summary>
        public void AddNearbyJunk(ResourceJunk junk)
        {
            if (_nearbyJunks.Contains(junk)) return;
            _nearbyJunks.Add(junk);
            RefreshMiningSlots();
        }

        /// <summary>MiningToolTrigger가 Junk 레이어 이탈 시 호출.</summary>
        public void RemoveNearbyJunk(ResourceJunk junk)
        {
            StopAndRemoveFromMining(junk);
            _nearbyJunks.Remove(junk);
            RefreshMiningSlots();
        }

        // ─── Mining Slot Management ──────────────────────────────────

        /// <summary>
        /// 가장 가까운 maxMiningSlots개 가용 정크만 채굴 활성화.
        /// 채굴 완료 또는 이탈 시 자동으로 다음 정크 활성화.
        /// </summary>
        private void RefreshMiningSlots()
        {
            // IsClaimed 필터: 본인이 점유했거나 비점유 상태만 포함
            List<ResourceJunk> targets = _nearbyJunks
                .Where(j => j.IsAvailable && (!j.IsClaimed || j.IsClaimedBy(this)))
                .OrderBy(j => Vector3.Distance(transform.position, j.transform.position))
                .Take(maxMiningSlots)
                .ToList();

            // 대상에서 빠진 채굴 중 정크 중단
            foreach (ResourceJunk j in _miningJunks.ToList())
            {
                if (!targets.Contains(j))
                    StopAndRemoveFromMining(j);
            }

            // 대상이지만 아직 채굴 안 한 정크 시작
            foreach (ResourceJunk j in targets)
            {
                if (!_miningJunks.Contains(j))
                {
                    MiningToolLevel tool  = UpgradeManager.Instance?.CurrentTool;
                    float           speed = tool?.mineSpeed ?? 1f;
                    if (j.StartMining(this, OnJunkMined, speed))
                    {
                        _miningJunks.Add(j);
                        
                    }
                }
            }
        }

        /// <summary>채굴 완료 콜백 — 다음 정크를 자동 활성화한다.</summary>
        private void OnJunkMined(ResourceJunk junk)
        {
            _miningJunks.Remove(junk);
            _nearbyJunks.Remove(junk);
            MiningToolLevel tool  = UpgradeManager.Instance?.CurrentTool;
            AudioManager.Instance?.PlayMineSFX(tool?.mineClip);
            RefreshMiningSlots();
        }

        private void StopAndRemoveFromMining(ResourceJunk junk)
        {
            if (_miningJunks.Remove(junk))
                junk.StopMining();
        }
    }
}
