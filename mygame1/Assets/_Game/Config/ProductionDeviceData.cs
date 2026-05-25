using System;
using UnityEngine;

namespace _Game.Config
{
    /// <summary>
    /// 生产设备数据定义 ScriptableObject。
    /// 描述一个可放置的自动化生产设备的输入/输出/速度/燃料需求。
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Production Device")]
    public class ProductionDeviceData : ScriptableObject
    {
        [Header("基础信息")]
        public string deviceName;
        public Sprite icon;
        [TextArea(2, 3)]
        public string description;
        public WorkstationTier tier;

        [Header("建造")]
        [Tooltip("放置后生成的 GameObject（带碰撞体）")]
        public GameObject builtPrefab;

        [Header("生产参数")]
        [Tooltip("该设备支持的原料→产物转换")]
        public ProductionRecipe[] recipes;
        [Tooltip("每轮生产间隔（秒，游戏时间）")]
        public float productionInterval = 5f;
        [Tooltip("每轮产出数量")]
        public int batchSize = 1;

        [Header("燃料（可选）")]
        public bool requiresFuel;
        public ItemData fuelItem;
        public float fuelPerCycle = 1f;

        [Header("工业加成")]
        [Tooltip("是否可以接入动力（水车/风车/发电机）")]
        public bool acceptsAutomation = true;
        [Tooltip("接入动力后速度倍率")]
        public float automationMultiplier = 2f;
    }

    /// <summary>
    /// 生产配方：原料 → 产物。ProductionDevice 专用。
    /// </summary>
    [Serializable]
    public struct ProductionRecipe
    {
        public ItemData input;
        public int inputCount;
        public ItemData output;
        public int outputCount;
        public float baseTime;
    }
}
