using UnityEngine;
using _Game.Config;
using _Game.Core;
using _Game.Systems.WorldContainer;

namespace _Game.Systems.Inventory
{
    /// <summary>
    /// 背包测试脚本（开发用，后续删除）
    /// I=添加物品  O=丢掉物品（丢地上）  F=快速拾取
    /// </summary>     
    public class InventoryTest : MonoBehaviour
    {
        public Inventory inventory;
        public ItemData testItem;

        void OnEnable()
        {
            InputRouter.BindKey(KeyCode.I, InputPriority.Debug, AddTestItem, this);
            InputRouter.BindKey(KeyCode.O, InputPriority.Debug, DropFirstItem, this);
            InputRouter.BindKey(KeyCode.F, InputPriority.Debug, PickupKey, this);
        }

        void OnDisable() { InputRouter.UnbindAll(this); }

        bool AddTestItem()
        {
            if (inventory != null && testItem != null)
            {
                inventory.AddItem(testItem, 1);
            }
            return true;
        }

        bool DropFirstItem()
        {
            if (inventory != null && inventory.placedItems.Count > 0)
            {
                var item = inventory.placedItems[0].itemData;
                inventory.DropItem(item, 1);
            }
            return true;
        }

        bool PickupKey()
        {
            PickupNearest();
            return true;
        }

        void PickupNearest()
        {
            // 检测附近的地面物品
            Collider[] colliders = Physics.OverlapSphere(transform.position, 2.5f, -1);
            WorldItem nearest = null;
            float nearestDist = float.MaxValue;

            foreach (var col in colliders)
            {
                var wi = col.GetComponent<WorldItem>();
                if (wi == null || wi.itemData == null) continue;

                float dist = Vector3.Distance(transform.position, col.transform.position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = wi;
                }
            }

            if (nearest != null && inventory != null)
            {
                int added = inventory.AddItem(nearest.itemData, nearest.count);
                if (added > 0)
                {
                    nearest.count -= added;
                    if (nearest.count <= 0)
                        Destroy(nearest.gameObject);
                }
            }
        }
    }
}
