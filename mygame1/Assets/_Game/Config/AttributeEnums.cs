using UnityEngine;

namespace _Game.Config
{
    /// <summary>
    /// 生存数值类型（用于 SurvivalStatChanged 事件）
    /// </summary>
    public enum SurvivalStatType
    {
        Health,
        Hunger,
        Thirst,
        Temperature
    }

    /// <summary>
    /// 生存异常状态
    /// </summary>
    public enum SurvivalStateType
    {
        None,
        Bleeding,       // 出血
        Infected,       // 感染
        Fracture,       // 骨折
        Hypothermia,    // 失温
        Overheated      // 过热
    }

    /// <summary>
    /// 物品效果类型
    /// </summary>
    public enum ItemEffectType
    {
        RestoreHealth,      // 恢复健康
        RestoreHunger,      // 恢复饥饿
        RestoreThirst,      // 恢复口渴
        RestoreTemperature, // 恢复体温
        CureBleeding,       // 治愈出血
        CureInfected,       // 治愈感染
        FixFracture,        // 修复骨折
        TemporaryWarmth     // 临时保暖
    }

    /// <summary>
    /// 物品效果数据结构
    /// </summary>
    [System.Serializable]
    public class ItemEffect
    {
        public ItemEffectType effectType;
        public float value;
        public float duration;
        public bool isInstant;
    }
}
