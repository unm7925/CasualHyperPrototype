using System;
using DG.Tweening;
using UnityEngine;

namespace IdleTycoon.Items
{
    /// <summary>
    /// 스택 안에 들어가는 단일 아이템 오브젝트.
    /// 목표 위치를 받아 부드럽게 Lerp로 이동한다.
    /// Object Pooling 대응: Spawn/Despawn 메서드를 통해 활성화/비활성화.
    /// </summary>
    public class StackItem : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float followSpeed = 15f;
        [SerializeField] private float rotationSpeed = 10f;

        [Header("Arrival")]
        [Tooltip("목표 슬롯 도달로 판정하는 거리 임계값")]
        [SerializeField] private float arrivalThreshold = 0.05f;

        public ItemData Data { get; private set; }

        /// <summary>
        /// 목표 슬롯에 도달했을 때 한 번만 발행되는 콜백.
        /// AssignSlot() 호출 시 자동으로 리셋되므로 슬롯이 바뀌면 재발행된다.
        /// </summary>
        public event Action OnReachedTarget;
        public ItemType ItemType {get; private set;}
        
        private Transform _targetSlot;
        private bool      _isFollowing;
        private bool      _hasReached;    // 이미 도달한 경우 재발행 방지

        // ─── Pool 진입점 ───────────────────────────────────────────

        public void Spawn(ItemData data, Transform slot)
        {
            Data = data;
            gameObject.SetActive(true);
            
            ItemType = data.itemType;
            AssignSlot(slot);
        }

        /// <summary>
        /// 풀 반환 전 모든 런타임 상태를 초기화한다.
        /// 이동 슬롯, 도달 플래그, 콜백 구독자, Data를 모두 제거한다.
        /// </summary>
        public void ResetState()
        {
            _targetSlot     = null;
            _isFollowing    = false;
            _hasReached     = false;
            OnReachedTarget = null;
            Data            = null;
        }

        /// <summary>
        /// StackItemPool이 씬에 있으면 풀로 반환하고,
        /// 없으면 기존 방식(SetActive false)으로 비활성화한다.
        /// </summary>
        public void Despawn()
        {
            if (StackItemPool.Instance != null)
                StackItemPool.Instance.Release(this);
            else
            {
                _isFollowing = false;
                _targetSlot  = null;
                gameObject.SetActive(false);
            }
        }

        // ─── Slot 할당 (스택 내 순서 변경 시에도 재호출) ──────────

        public void AssignSlot(Transform slot)
        {
            _targetSlot  = slot;
            _isFollowing = slot != null;
            _hasReached  = false;   // 슬롯 변경 시 도달 상태 리셋
        }

        // ─── 매 프레임 목표 슬롯을 향해 이동 ──────────────────────

        private void Update()
        {
            if (!_isFollowing || _targetSlot == null) return;

            transform.position = Vector3.Lerp(
                transform.position,
                _targetSlot.position,
                followSpeed * Time.deltaTime
            );

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                _targetSlot.rotation,
                rotationSpeed * Time.deltaTime
            );

            // 도달 판정 — 한 번만 발행
            if (!_hasReached &&
                Vector3.Distance(transform.position, _targetSlot.position) <= arrivalThreshold)
            {
                _hasReached = true;
                OnReachedTarget?.Invoke();
            }
        }
    }
}
