using UnityEngine;

namespace IdleTycoon.Core
{
    /// <summary>
    /// 카메라 팔로우 컴포넌트.
    /// 메인 카메라에 부착하고 target에 플레이어를 할당한다.
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3   offset       = new Vector3(10f, 15f, -10f);
        [SerializeField] private float     followSpeed  = 10f;

        private void LateUpdate()
        {
            if (target == null) return;

            Vector3 desired = target.position + offset;
            transform.position = Vector3.Lerp(transform.position, desired, followSpeed * Time.deltaTime);
        }
    }
}
