using IdleTycoon.Items;

namespace IdleTycoon.Core
{
    public interface IItemReceiver
    {
        bool CanReceive(ItemData itemData);
        void Receive(StackItem item);
    }

    public interface IItemProvider
    {
        bool HasItems();
        StackItem ProvideItem();
    }

    public interface IInteractable
    {
        void OnPlayerEnter(UnityEngine.Component player);
        void OnPlayerStay(UnityEngine.Component player);
        void OnPlayerExit(UnityEngine.Component player);
    }
}
