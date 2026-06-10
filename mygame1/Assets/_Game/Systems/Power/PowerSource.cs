using UnityEngine;
using _Game.Config;
using _Game.Core;
using _Game.Systems.Interaction;
using _Game.UI;

namespace _Game.Systems.Power
{
    /// <summary>
    /// 发电端组件。挂载在放置后的发电设备上。IInteractable 打开电源面板。
    /// </summary>
    public class PowerSource : MonoBehaviour, IInteractable
    {
        [Header("发电参数")]
        public PowerSourceType sourceType;
        public float maxOutput = 100f;

        [Header("燃料")]
        public bool requiresFuel;
        public float fuelPerHour = 1f;
        public string fuelItemName;
        public ItemData fuelItemData;

        [Header("限制")]
        public bool requiresOpenAir;
        public bool requiresWater;
        public bool daytimeOnly;

        [Header("噪音")]
        public float noiseRadius = 10f;

        [Header("耐久(预留)")]
        public float maxDurability = 100f;
        public float durability = 100f;

        float _fuelRemaining;
        float _fuelTimer;
        bool _isActive;

        public bool IsActive => _isActive;
        public float CurrentOutput => _isActive ? maxOutput : 0f;
        public float FuelRemaining => _fuelRemaining;
        public float MaxOutput => maxOutput;
        public float DurabilityPercent => maxDurability > 0f ? durability / maxDurability : 1f;

        // IInteractable
        string IInteractable.InteractionPrompt => $"{sourceType} [E]";
        float IInteractable.InteractionTime => 0f;
        bool IInteractable.IsInteractable => enabled;

        void IInteractable.OnInteract(GameObject interactor)
        {
            PowerSourceUI.Show(this);
        }

        Inventory.Inventory _inventory;

        void Start()
        {
            _fuelRemaining = requiresFuel ? 1f : 999f;
            _inventory = ServiceLocator.Get<Inventory.Inventory>();
        }

        void Update()
        {
            bool conditionsMet = true;

            if (daytimeOnly)
            {
                var sun = FindObjectOfType<Light>();
                if (sun != null && sun.intensity < 0.1f)
                    conditionsMet = false;
            }

            if (requiresFuel)
            {
                _fuelTimer += UnityEngine.Time.deltaTime;
                float consumeInterval = 3600f / fuelPerHour;
                if (_fuelTimer >= consumeInterval)
                {
                    _fuelTimer = 0f;
                    _fuelRemaining -= 1f;
                }

                // 燃料耗尽时自动从玩家背包补充
                if (_fuelRemaining <= 0f && _inventory != null && fuelItemData != null)
                {
                    int count = _inventory.CountItemByName(fuelItemData.itemName);
                    if (count > 0)
                    {
                        _inventory.RemoveItemByName(fuelItemData.itemName, 1);
                        _fuelRemaining += fuelItemData.fuelValue > 0f ? fuelItemData.fuelValue : 1f;
                    }
                }

                if (_fuelRemaining <= 0f)
                    conditionsMet = false;
            }

            _isActive = conditionsMet;
        }

        public void AddFuel(float amount)
        {
            _fuelRemaining += amount;
        }
    }
}
