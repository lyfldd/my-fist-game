using System.Collections;
using UnityEngine;
using _Game.Config;
using _Game.Core;
using _Game.Systems.Interaction;
using _Game.Systems.Inventory;
using _Game.Systems.ItemGraph;

namespace _Game.Systems.Crafting
{
    /// <summary>
    /// 生产设备运行时组件。挂载在放置后的设备 GameObject 上。
    /// IInteractable（E键打开设备面板）+ 自运转（消耗原料→产出）。
    /// 电源状态由 PowerConsumer 通知，决定运转模式。
    /// </summary>
    public class ProductionDevice : MonoBehaviour, IInteractable
    {
        [SerializeField] ProductionDeviceData _data;

        [Header("输入/输出槽")]
        [SerializeField] Inventory.InventoryContainer _inputSlot;
        [SerializeField] Inventory.InventoryContainer _outputSlot;

        [Header("流水线")]
        [SerializeField] ProductionDevice _outputDestination;

        float _cycleTimer;
        float _fuelRemaining;
        bool _isElectricPowered;
        bool _isCoalPowered;
        Inventory.Inventory _inventoryFallback;

        public ProductionDeviceData Data => _data;
        public Inventory.InventoryContainer OutputSlot => _outputSlot;
        public Inventory.InventoryContainer InputSlot => _inputSlot;
        public float FuelRemaining => _fuelRemaining;
        public bool IsElectricPowered => _isElectricPowered;
        public bool IsCoalPowered => _isCoalPowered;
        public bool IsRunning => _isElectricPowered || _isCoalPowered ||
            (_data != null && !_data.requiresFuel && GetComponent<Power.PowerConsumer>() == null);
        public ProductionDevice OutputDestination
        {
            get => _outputDestination;
            set => _outputDestination = value;
        }

        // ---- IInteractable ----
        string IInteractable.InteractionPrompt => $"使用 {_data.deviceName}";
        float IInteractable.InteractionTime => 0.3f;
        bool IInteractable.IsInteractable => enabled && gameObject.activeInHierarchy;

        void IInteractable.OnInteract(GameObject interactor)
        {
            EventBus.Publish(new DeviceOpenedEvent(this));
        }

        void Awake()
        {
            if (_inventoryFallback == null)
                _inventoryFallback = ServiceLocator.Get<Inventory.Inventory>();
        }

        public void Init(ProductionDeviceData data)
        {
            _data = data;

            if (_inputSlot == null)
                _inputSlot = new Inventory.InventoryContainer
                {
                    containerName = $"{data.deviceName} 输入槽",
                    gridWidth = 4, gridHeight = 3
                };
            if (_outputSlot == null)
                _outputSlot = new Inventory.InventoryContainer
                {
                    containerName = $"{data.deviceName} 输出槽",
                    gridWidth = 4, gridHeight = 2
                };

            if (_data.requiresFuel && _data.fuelItem != null)
                _fuelRemaining = _data.fuelPerCycle * 3f;
        }

        /// <summary>
        /// 由 PowerConsumer 调用，通知当前电力/煤模式状态。
        /// </summary>
        public void NotifyPowerState(bool electricPowered, bool coalPowered)
        {
            _isElectricPowered = electricPowered;
            _isCoalPowered = coalPowered;
        }

        void Update()
        {
            if (_data == null) return;

            float interval = _data.productionInterval;

            // 通电加速
            if (_isElectricPowered)
            {
                var consumer = GetComponent<Power.PowerConsumer>();
                if (consumer != null)
                    interval /= consumer.electricSpeedMultiplier;
            }

            _cycleTimer += UnityEngine.Time.deltaTime;
            if (_cycleTimer >= interval)
            {
                _cycleTimer = 0f;
                TryProduce();
            }
        }

        void TryProduce()
        {
            // 研究门控
            var researchMgr = ServiceLocator.Get<ChemicalResearchManager>();
            if (researchMgr != null && !researchMgr.IsDeviceUnlocked(_data.deviceName))
                return;

            var recipes = GetActiveRecipes();
            if (recipes == null || recipes.Length == 0) return;

            var consumer = GetComponent<Power.PowerConsumer>();

            // 有 PowerConsumer 的设备：必须通电或烧煤才能运转
            if (consumer != null)
            {
                if (!_isElectricPowered && !_isCoalPowered)
                    return;
            }

            // 通电：跳过燃料消耗
            if (!_isElectricPowered)
            {
                if (_data.requiresFuel)
                {
                    if (_fuelRemaining <= 0f)
                    {
                        if (!ConsumeFuel())
                        {
                            EventBus.Publish(new DeviceFuelDepletedEvent(gameObject));
                            return;
                        }
                    }
                    _fuelRemaining -= _data.fuelPerCycle;
                }
            }

            // 遍历配方（优先走 RecipeData，兜底 ProductionRecipe）
            foreach (var recipe in recipes)
            {
                // 配方级研究门控（后期/终局）
                if (!string.IsNullOrEmpty(recipe.recipeId))
                {
                    if (researchMgr != null && !researchMgr.IsRecipeUnlocked(recipe.recipeId))
                        continue;
                }

                // 多材料输入检查
                if (recipe.inputs != null && recipe.inputs.Length > 0)
                {
                    bool hasAll = true;
                    foreach (var req in recipe.inputs)
                    {
                        if (req.itemData == null || !HasInputItem(req.itemData, req.count))
                        { hasAll = false; break; }
                    }
                    if (hasAll)
                    {
                        foreach (var req in recipe.inputs)
                            RemoveInputItem(req.itemData, req.count);
                        PlaceOutput(recipe.output, recipe.outputCount);
                        EventBus.Publish(new ProductionCycleEvent(
                            gameObject, recipe.output, recipe.outputCount));
                        return;
                    }
                }
                // 单材料输入（兼容旧数据）
                else if (recipe.input != null && HasInputItem(recipe.input, recipe.inputCount))
                {
                    RemoveInputItem(recipe.input, recipe.inputCount);
                    PlaceOutput(recipe.output, recipe.outputCount);
                    EventBus.Publish(new ProductionCycleEvent(
                        gameObject, recipe.output, recipe.outputCount));
                    return;
                }
            }
        }

        /// <summary>
        /// 获取设备的所有配方：优先从图谱/RecipeCatalog 查询，兜底使用 ProductionDeviceData 内嵌配方
        /// </summary>
        ProductionRecipe[] GetActiveRecipes()
        {
            // 已有内嵌配方直接使用
            if (_data.recipes != null && _data.recipes.Length > 0)
                return _data.recipes;

            // 从图谱查询工业配方
            var graph = ItemGraphManager.Instance;
            if (graph != null)
            {
                var catalogRecipes = graph.GetRecipesForDevice(_data.deviceName);
                if (catalogRecipes.Length > 0)
                {
                    var result = new ProductionRecipe[catalogRecipes.Length];
                    for (int i = 0; i < catalogRecipes.Length; i++)
                    {
                        var r = catalogRecipes[i];
                        result[i] = new ProductionRecipe
                        {
                            input = r.materials != null && r.materials.Length > 0 ? r.materials[0].itemData : null,
                            inputCount = r.materials != null && r.materials.Length > 0 ? r.materials[0].count : 0,
                            output = r.resultItem,
                            outputCount = r.resultCount,
                            baseTime = r.craftTime
                        };
                    }
                    return result;
                }
            }

            return new ProductionRecipe[0];
        }

        bool ConsumeFuel()
        {
            if (_data.fuelItem == null) return false;
            if (_inputSlot != null && _inputSlot.GetItemCount(_data.fuelItem) >= 1)
            {
                _inputSlot.RemoveItem(_data.fuelItem, 1);
                _fuelRemaining += _data.fuelPerCycle * 10f;
                return true;
            }
            return false;
        }

        bool HasInputItem(ItemData item, int count)
        {
            if (_inputSlot != null) return _inputSlot.GetItemCount(item) >= count;
            return _inventoryFallback != null && _inventoryFallback.HasItem(item, count);
        }

        void RemoveInputItem(ItemData item, int count)
        {
            if (_inputSlot != null)
                _inputSlot.RemoveItem(item, count);
            else if (_inventoryFallback != null)
                _inventoryFallback.RemoveItem(item, count);
        }

        void PlaceOutput(ItemData item, int count)
        {
            if (_outputDestination != null && _outputDestination.InputSlot != null)
            {
                int forwarded = _outputDestination.InputSlot.AddItem(item, count, float.MaxValue);
                if (forwarded > 0)
                {
                    EventBus.Publish(new ProductionCycleEvent(
                        gameObject, item, forwarded));
                    return;
                }
                EventBus.Publish(new DeviceOutputFullEvent(_outputDestination.gameObject));
            }

            if (_outputSlot != null)
            {
                int added = _outputSlot.AddItem(item, count, float.MaxValue);
                if (added <= 0)
                    EventBus.Publish(new DeviceOutputFullEvent(gameObject));
            }
            else if (_inventoryFallback != null)
            {
                _inventoryFallback.AddItem(item, count);
            }
        }

        // ============================================================
        // 存档系统接口
        // ============================================================

        public SaveLoad.ProductionSaveData GetSaveData()
        {
            var guid = GetComponent<SaveLoad.PersistentGUID>();
            var psd = new SaveLoad.ProductionSaveData
            {
                guid = guid != null ? guid.Guid : "",
                deviceName = _data != null ? _data.deviceName : "",
                fuelRemaining = _fuelRemaining,
                cycleTimer = _cycleTimer,
            };

            // 输入槽物品
            if (_inputSlot != null && _inputSlot.placedItems.Count > 0)
            {
                foreach (var pi in _inputSlot.placedItems)
                {
                    if (pi.itemData == null) continue;
                    psd.inputInstanceId = pi.instanceId;
                    psd.inputItemName = pi.itemData.itemName;
                    psd.inputCount = pi.count;
                    break; // 只存第一个物品（当前设备单物品输入）
                }
            }

            // 产出槽物品
            if (_outputSlot != null && _outputSlot.placedItems.Count > 0)
            {
                foreach (var pi in _outputSlot.placedItems)
                {
                    if (pi.itemData == null) continue;
                    psd.outputInstanceId = pi.instanceId;
                    psd.outputItemName = pi.itemData.itemName;
                    psd.outputCount = pi.count;
                    break;
                }
            }

            // 输出链接目标
            if (_outputDestination != null)
            {
                var destGuid = _outputDestination.GetComponent<SaveLoad.PersistentGUID>();
                if (destGuid != null) psd.outputDestinationGuid = destGuid.Guid;
            }

            return psd;
        }

        public void RestoreFromSave(SaveLoad.ProductionSaveData psd, SaveLoad.ItemCatalog itemCatalog)
        {
            if (psd == null) return;

            _fuelRemaining = psd.fuelRemaining;
            _cycleTimer = psd.cycleTimer;

            // 恢复输入槽
            if (_inputSlot != null && !string.IsNullOrEmpty(psd.inputItemName) && itemCatalog != null)
            {
                _inputSlot.placedItems.Clear();
                var item = itemCatalog.Find(psd.inputItemName);
                if (item != null && psd.inputCount > 0)
                {
                    _inputSlot.placedItems.Add(new Inventory.PlacedItem
                    {
                        instanceId = psd.inputInstanceId,
                        itemData = item,
                        count = psd.inputCount,
                    });
                }
            }

            // 恢复产出槽
            if (_outputSlot != null && !string.IsNullOrEmpty(psd.outputItemName) && itemCatalog != null)
            {
                _outputSlot.placedItems.Clear();
                var item = itemCatalog.Find(psd.outputItemName);
                if (item != null && psd.outputCount > 0)
                {
                    _outputSlot.placedItems.Add(new Inventory.PlacedItem
                    {
                        instanceId = psd.outputInstanceId,
                        itemData = item,
                        count = psd.outputCount,
                    });
                }
            }

            // 输出链接恢复（由 SaveLoadManager 集中处理的步骤）
            _pendingRestoreDestGuid = psd.outputDestinationGuid;
        }

        /// <summary> 待恢复的输出链接 GUID（加载第二阶段处理） </summary>
        [System.NonSerialized] public string _pendingRestoreDestGuid;
    }
}
