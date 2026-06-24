using UnityEngine;
using _Game.Config;
using _Game.Core;

namespace _Game.Systems.Electronic
{
    /// <summary>
    /// 电子设备运行时组件。
    /// 绑定物品 instanceId，精确读取对应物品的耐久/类型。
    /// </summary>
    public class ElectronicDevice : MonoBehaviour
    {
        [Header("设备类型")]
        public ElectronicDeviceType deviceType;

        [Header("运行时状态")]
        public bool isOn;
        public float currentEnergy = 100f;
        public float maxEnergy = 100f;
        public float energyPerSecond = 0.1f;

        // 内部
        private Light _spotLight;
        private Inventory.Inventory _inventory;
        private int _boundInstanceId;  // 绑定的物品实例 ID

        public float EnergyRatio => maxEnergy > 0f ? Mathf.Clamp01(currentEnergy / maxEnergy) : 0f;
        public bool IsEnergyDepleted => currentEnergy <= 0f;

        /// <summary> 绑定到指定物品实例（装备时调用） </summary>
        public void Bind(int instanceId) => _boundInstanceId = instanceId;

        void Awake()
        {
            _inventory = GetComponent<Inventory.Inventory>();
        }

        void Start()
        {
            if (deviceType == ElectronicDeviceType.Flashlight)
            {
                _spotLight = GetComponentInChildren<Light>();
                if (_spotLight == null)
                {
                    var go = new GameObject("FlashlightSpot");
                    go.transform.SetParent(transform);
                    go.transform.localPosition = Vector3.zero;
                    _spotLight = go.AddComponent<Light>();
                    _spotLight.type = LightType.Spot;
                    _spotLight.range = 30f;
                    _spotLight.spotAngle = 60f;
                    _spotLight.intensity = 2f;
                }
                if (_spotLight != null) _spotLight.enabled = false;
            }
        }

        void Update()
        {
            if (!isOn) return;
            currentEnergy -= energyPerSecond * UnityEngine.Time.deltaTime;
            if (currentEnergy <= 0f) { currentEnergy = 0f; TurnOff(); return; }
            if (deviceType == ElectronicDeviceType.Flashlight && _spotLight != null)
            {
                float ratio = GetDurabilityRatio();
                _spotLight.intensity = Mathf.Lerp(0.3f, 2f, ratio);
            }
        }

        public void Toggle() { if (isOn) TurnOff(); else TurnOn(); }

        public void TurnOn()
        {
            if (IsEnergyDepleted) return;
            isOn = true;
            if (deviceType == ElectronicDeviceType.Flashlight && _spotLight != null)
                _spotLight.enabled = true;
        }

        public void TurnOff()
        {
            isOn = false;
            if (deviceType == ElectronicDeviceType.Flashlight && _spotLight != null)
                _spotLight.enabled = false;
        }

        public bool TryIgnite()
        {
            if (deviceType != ElectronicDeviceType.Lighter || IsEnergyDepleted) return false;
            float ratio = GetDurabilityRatio();
            if (Random.value > 0.5f + 0.5f * ratio) return false;
            currentEnergy -= 1f;
            if (currentEnergy <= 0f) { currentEnergy = 0f; TurnOff(); }
            return true;
        }

        public float GetCompassOffset()
        {
            float ratio = GetDurabilityRatio();
            return Random.Range(-10f * (1f - ratio), 10f * (1f - ratio));
        }

        public bool IsWatchReadable() => GetDurabilityRatio() > 0.1f;

        public bool ReplaceBattery()
        {
            if (_inventory == null) return false;
            foreach (var c in _inventory.containers)
                foreach (var pi in c.placedItems)
                    if (pi.itemData != null && pi.itemData.electronicDeviceType == ElectronicDeviceType.Battery
                        && pi.count > 0)
                    {
                        _inventory.RemoveItem(pi.itemData, 1);
                        currentEnergy = maxEnergy;
                        return true;
                    }
            return false;
        }

        float GetDurabilityRatio()
        {
            // 优先通过 instanceId 精确查找
            if (_boundInstanceId > 0 && _inventory != null)
            {
                var p = _inventory.FindPlacedItem(_boundInstanceId);
                if (p.HasValue && p.Value.itemData != null && p.Value.itemData.hasDurability
                    && p.Value.itemData.maxDurability > 0f)
                {
                    float cur = p.Value.itemDurability;
                    if (cur <= 0f) return 1f;
                    return Mathf.Clamp01(cur / p.Value.itemData.maxDurability);
                }
            }
            // fallback: 按 deviceType 遍历
            if (_inventory == null) return 1f;
            foreach (var c in _inventory.containers)
                foreach (var pi in c.placedItems)
                    if (pi.itemData != null && pi.itemData.electronicDeviceType == deviceType
                        && pi.itemData.hasDurability && pi.itemData.maxDurability > 0f)
                    {
                        if (pi.itemDurability <= 0f) return 1f;
                        return Mathf.Clamp01(pi.itemDurability / pi.itemData.maxDurability);
                    }
            return 1f;
        }
    }
}
