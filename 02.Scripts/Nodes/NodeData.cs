using UnityEngine;
using IdleTycoon.Items;

namespace IdleTycoon.Nodes
{
    [CreateAssetMenu(fileName = "NewNodeData", menuName = "IdleTycoon/Node Data")]
    public class NodeData : ScriptableObject
    {
        [Header("Output")]
        public ItemData outputItem;

        [Header("Mining")]
        [Tooltip("아이템 1개 채굴에 걸리는 시간 (초)")]
        public float mineInterval = 1f;

        [Tooltip("채굴된 정크가 다시 활성화되기까지 걸리는 시간 (초)")]
        public float respawnTime = 3f;
    }
}
