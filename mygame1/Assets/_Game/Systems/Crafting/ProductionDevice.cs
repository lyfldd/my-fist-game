using System.Collections;
using UnityEngine;
using _Game.Config;
using _Game.Core;
using _Game.Systems.Interaction;
using _Game.Systems.Inventory;

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
                _inventoryFallback = Object.FindObjectOfType<Inventory.Inventory>();
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
            if (_data.recipes == null || _data.recipes.Length == 0) return;

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

            // 遍历配方
            foreach (var recipe in _data.recipes)
            {
                if (HasInputItem(recipe.input, recipe.inputCount))
                {
                    RemoveInputItem(recipe.input, recipe.inputCount);
                    PlaceOutput(recipe.output, recipe.outputCount);
                    EventBus.Publish(new ProductionCycleEvent(
                        gameObject, recipe.output, recipe.outputCount));
                    return;
                }
            }
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
    }
}
