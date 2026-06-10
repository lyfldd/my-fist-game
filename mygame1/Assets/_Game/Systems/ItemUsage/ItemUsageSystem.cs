using UnityEngine;
using _Game.Core;
using _Game.Systems.Survival;

namespace _Game.Systems.ItemUsage
{
    /// <summary>
    /// 物品使用系统
    /// 订阅 ItemUsedEvent，统一处理：扣物品 + 加效果
    /// 解耦 UI 层（QuickItemBar）与系统层（Inventory + Survival）
    /// </summary>
    public class ItemUsageSystem : MonoBehaviour
    {
        private _Game.Systems.Inventory.Inventory _inventory;
        private SurvivalSystem _survival;

        private void Start()
        {
            _inventory = ServiceLocator.Get<_Game.Systems.Inventory.Inventory>();
            _survival = ServiceLocator.Get<SurvivalSystem>();
            EventBus.Subscribe<ItemUsedEvent>(OnItemUsed);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<ItemUsedEvent>(OnItemUsed);
        }

        private void OnItemUsed(ItemUsedEvent evt)
        {
            if (evt.ItemData == null) return;

            if (_inventory != null)
                _inventory.RemoveItem(evt.ItemData, evt.Count);

            if (_survival != null && evt.ItemData.itemEffects != null)
            {
                foreach (var eff in evt.ItemData.itemEffects)
                    _survival.ApplyItemEffect(eff);
            }
        }
    }
}
