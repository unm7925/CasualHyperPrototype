using UnityEngine;

namespace IdleTycoon.Items
{
    [CreateAssetMenu(fileName = "NewItemData", menuName = "IdleTycoon/Item Data")]
    public class ItemData : ScriptableObject
    {
        [Header("Identity")]
        public string itemName;
        public ItemType itemType;

        [Header("Prefab")]
        public GameObject prefab;

        [Header("Economy")]
        public int sellValue = 1;
    }

    public enum ItemType
    {
        Stone,
        Handcuff,
        Money
    }
}
