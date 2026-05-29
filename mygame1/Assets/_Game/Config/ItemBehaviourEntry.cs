using System;
using UnityEngine;

namespace _Game.Config
{
    /// <summary>
    /// 物品行为条目 — 描述物品的一个功能行为
    /// 每个条目对应一个脚本/系统，放入 ItemData.behaviours 列表中
    /// 解决 "ItemData 字段无限膨胀" 的问题
    /// </summary>
    [Serializable]
    public class ItemBehaviourEntry
    {
        [Tooltip("行为名称（人类可读）")]
        public string behaviourName;

        [Tooltip("归属的游戏系统")]
        public GameSystem system;

        [Tooltip("运行时挂载的组件全类型名（如 NightVisionEffect），留空=无需脚本（纯数据驱动）")]
        public string componentTypeName;

        [Tooltip("脚本/功能是否已实现")]
        public bool implemented;

        [Tooltip("备忘")]
        public string notes;
    }
}
