using UnityEngine;
using _Game.Config;
using _Game.Core;

namespace _Game.Systems.Electronic
{
    /// <summary>
    /// 电子设备运行时组件（前置C）。
    /// 挂载到持有电子设备的玩家/实体上，驱动运行时行为。
    /// 从背包中查找对应 ItemData 并消耗耐久。
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
        private Light _spotLight;          // 手电筒
        private float _lighterTimer;       // 打火机计时
        private Inventory.Inventory _inventory;

        public float EnergyRatio => maxEnergy > 0f ? Mathf.Clamp01(currentEnergy / maxEnergy) : 0f;
        public bool IsEnergyDepleted => currentEnergy <= 0f;

        void Awake()
        {
            _inventory = GetComponent<Inventory.Inventory>();
        }

        void Start()
        {
            // 手电筒：查找或创建 SpotLight 子物体
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

            // 消耗能量
            currentEnergy -= energyPerSecond * UnityEngine.Time.deltaTime;
            if (currentEnergy <= 0f)
            {
                currentEnergy = 0f;
                TurnOff();
                return;
            }

            // 手电筒：光强随耐久衰减
            if (deviceType == ElectronicDeviceType.Flashlight && _spotLight != null)
            {
                float ratio = GetDurabilityRatio();
                _spotLight.intensity = Mathf.Lerp(0.3f, 2f, ratio);
            }
        }

        // ============================================================
        // 开关
        // ============================================================

        public void Toggle()
        {
            if (isOn) TurnOff();
            else TurnOn();
        }

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

        // ============================================================
        // 打火机：短暂点火
        // ============================================================

        /// <summary> 尝试短暂点火，返回是否成功（0%耐久=概率失败） </summary>
        public bool TryIgnite()
        {
            if (deviceType != ElectronicDeviceType.Lighter) return false;
            if (IsEnergyDepleted) return false;

            float ratio = GetDurabilityRatio();
            // 耐久越低成功率越低，最低50%
            float successChance = 0.5f + 0.5f * ratio;
            bool success = Random.value < successChance;

            if (success)
            {
                currentEnergy -= 1f;
                if (currentEnergy <= 0f) { currentEnergy = 0f; TurnOff(); }
            }

            return success;
        }

        // ============================================================
        // 指南针 / 手表（纯数据查询，无Update行为）
        // ============================================================

        /// <summary> 指南针方向偏移（度），0耐久=±10°随机 </summary>
        public float GetCompassOffset()
        {
            float ratio = GetDurabilityRatio();
            float maxJitter = 10f * (1f - ratio);
            return Random.Range(-maxJitter, maxJitter);
        }

        /// <summary> 手表是否可读（耐久>0） </summary>
        public bool IsWatchReadable()
        {
            return GetDurabilityRatio() > 0.1f;
        }

        // ============================================================
        // 替换电池：从背包消耗电池组
        // ============================================================

        public bool ReplaceBattery()
        {
            if (_inventory == null) return false;

            // 查找电池组
            foreach (var c in _inventory.containers)
                foreach (var pi in c.placedItems)
                    if (pi.itemData != null && pi.itemData.itemName == "电池组" && pi.count > 0)
                    {
                        _inventory.RemoveItem(pi.itemData, 1);
                        currentEnergy = maxEnergy;
                        return true;
                    }
            return false;
        }

        // ============================================================
        // 耐久查询
        // ============================================================

        float GetDurabilityRatio()
        {
            if (_inventory == null) return 1f;
            // 遍历背包找匹配的设备 ItemData
            string targetName = deviceType switch
            {
                ElectronicDeviceType.Flashlight => "手电筒",
                ElectronicDeviceType.Lighter    => "打火机",
                ElectronicDeviceType.Compass    => "指南针",
                ElectronicDeviceType.Watch      => "手表",
                _ => null
            };
            if (string.IsNullOrEmpty(targetName)) return 1f;

            foreach (var c in _inventory.containers)
                foreach (var pi in c.placedItems)
                    if (pi.itemData != null && pi.itemData.itemName == targetName
                        && pi.itemData.hasDurability && pi.itemData.maxDurability > 0f)
                    {
                        if (pi.itemDurability <= 0f) return 1f; // 未初始化
                        return Mathf.Clamp01(pi.itemDurability / pi.itemData.maxDurability);
                    }
            return 1f; // 未装备该设备
        }
    }
}
