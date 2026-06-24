using _Game.Systems.AIBot;
using System;
using UnityEngine;

namespace _Game.Config
{
    /// <summary>
    /// AIBot 武器映射表 — 替代硬编码字符串。
    /// Tools → AIBot 武器映射编辑器 可编辑。
    /// </summary>
    [CreateAssetMenu(menuName = "Game/AI Bot Weapon Mapping")]
    public class AIBotWeaponMapping : ScriptableObject
    {
        public Entry[] entries;

        [Serializable]
        public class Entry
        {
            public RightArmWeapon rightWeapon;
            public LeftArmWeapon leftWeapon;
            public ItemData weaponItem;
            public ItemData ammoItem;
            public ItemData leftWeaponItem;
        }

        public Entry GetRight(RightArmWeapon w)
        {
            if (entries != null)
                foreach (var e in entries)
                    if (e.rightWeapon == w) return e;
            return null;
        }

        public Entry GetLeft(LeftArmWeapon w)
        {
            if (entries != null)
                foreach (var e in entries)
                    if (e.leftWeapon == w) return e;
            return null;
        }
    }
}
