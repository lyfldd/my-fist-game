using System;

namespace _Game.Systems.SaveLoad
{
    /// <summary>
    /// 存档槽元数据。与 save.json 分开存储为 meta.json，加载前即可读取（UI 展示用）。
    /// </summary>
    [Serializable]
    public class SaveSlotMeta
    {
        public int slotIndex;              // 0~4
        public int saveVersion;            // 对应 SaveData.version
        public int worldGenVersion;        // 对应 SaveData.worldGenVersion
        public string saveDateTime;        // ISO 8601
        public float totalPlayTime;        // 累计游戏秒数
        public float gameDays;             // 游戏内天数
        public int playerLevel;            // 玩家等级（UI 显示用）
        public string thumbnailPath;       // thumb.png 相对路径

        // 死亡状态
        public bool isDead;
        public float deathGameDay;

        /// <summary> 自动生成 UI 描述文本 </summary>
        public string GetDescription()
        {
            if (isDead) return $"已死亡 — 第{deathGameDay:F0}天";
            return $"第{gameDays:F0}天 Lv.{playerLevel}";
        }

        /// <summary> 槽位是否可加载（永久死亡模式下 isDead=true 不可加载） </summary>
        public bool CanLoad(bool permadeath)
        {
            return !(permadeath && isDead);
        }
    }
}
