using UnityEngine;
using _Game.Core;
using _Game.Systems.Interaction;

namespace _Game.Systems.WorldContainer
{
    /// <summary>
    /// 地面物品的交互接口
    /// 按 E 可查看物品信息，按 F 快速拾取
    /// </summary>
    [RequireComponent(typeof(WorldItem))]
    public class GroundItemInteract : MonoBehaviour, IInteractable
    {
        private WorldItem _worldItem;

        void Awake()
        {
            _worldItem = GetComponent<WorldItem>();
        }

        public string InteractionPrompt
        {
            get
            {
                if (_worldItem == null || _worldItem.itemData == null) return "物品";
                string name = _worldItem.itemData.itemName;
                if (_worldItem.count > 1) name += $" x{_worldItem.count}";
                return name;
            }
        }

        public float InteractionTime => 0f;  // 瞬间交互
        public bool IsInteractable => true;

        public void OnInteract(GameObject interactor)
        {
            if (_worldItem == null || _worldItem.itemData == null) return;

            var inventory = interactor.GetComponent<_Game.Systems.Inventory.Inventory>();
            if (inventory == null) return;

            int added = inventory.AddItem(_worldItem.itemData, _worldItem.count);
            if (added > 0)
            {
                EventBus.Publish(new InventoryChanged("added", _worldItem.itemData.itemName, added));
                _worldItem.count -= added;

                if (_worldItem.count <= 0)
                {
                    Destroy(gameObject);
                }
                else
                {
                    // 更新显示
                    InteractionPrompt.ToString(); // 下次交互更新提示
                }
            }
            else
            {
                EventBus.Publish(new InventoryChanged("slot_full", _worldItem.itemData.itemName, 0));
            }
        }
    }
}
