using UnityEngine;
using IdleTycoon.Items;

namespace IdleTycoon.Machines
{
    [CreateAssetMenu(fileName = "NewMachineData", menuName = "IdleTycoon/Machine Data")]
    public class MachineData : ScriptableObject
    {
        [Header("Conversion")]
        public ItemType  inputItemType;
        public ItemData  outputItem;

        [Header("Timing")]
        [Tooltip("아이템 1개 변환에 걸리는 시간 (초)")]
        public float processTime     = 2f;

        [Tooltip("플레이어에게서 아이템을 가져오는 간격 (초)")]
        public float transferInterval = 0.5f;

        [Header("Capacity")]
        [Tooltip("아웃풋 존에 쌓일 수 있는 최대 아이템 수 증가량")]
        public int maxOutputCapacity = 10;
    }
}
