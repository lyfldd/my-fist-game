using System;
using System.Collections.Generic;
using UnityEngine;

namespace _Game.Systems.SaveLoad
{
    /// <summary>
    /// 存档版本迁移框架。
    ///
    /// 每个迁移是纯函数: SaveData v(N) → SaveData v(N+1)。
    /// 加载时检测 SaveData.version，自动串行运行迁移链(1→2→3→...)。
    ///
    /// 新增字段时的迁移示例:
    ///   游戏更新后 SaveData.version 递增到 2，新增字段 totalPlayTime。
    ///   旧存档(v1)没有这个字段 → 反序列化后为 0 → 在 v1→v2 迁移中
    ///   可设置默认值（如基于 saveDateTime 估算）。
    ///
    /// 使用: 在 MigrationChain 字典中注册版本号 → 迁移函数。
    /// </summary>
    public static class SaveMigration
    {
        /// <summary> 当前代码支持的最新存档版本 </summary>
        public const int CURRENT_VERSION = 1;

        /// <summary> 版本号 → 迁移函数（vN → vN+1） </summary>
        private static readonly Dictionary<int, Func<SaveData, SaveData>> _migrationChain = new Dictionary<int, Func<SaveData, SaveData>>
        {
            // 示例（Phase 5 实际使用时取消注释）:
            // { 1, MigrateV1ToV2 },
            // { 2, MigrateV2ToV3 },
        };

        /// <summary>
        /// 将存档迁移到当前版本。如果已经是当前版本，直接返回原对象。
        /// </summary>
        public static SaveData Migrate(SaveData data)
        {
            if (data == null) return null;

            int startVersion = data.version;
            while (data.version < CURRENT_VERSION)
            {
                int fromVersion = data.version;
                if (!_migrationChain.TryGetValue(fromVersion, out var migrateFunc))
                {
                    Debug.LogError($"[SaveMigration] 缺少迁移函数 v{fromVersion}→v{fromVersion + 1}，停止迁移");
                    return null;
                }

                data = migrateFunc(data);
                if (data == null)
                {
                    Debug.LogError($"[SaveMigration] 迁移 v{fromVersion}→v{fromVersion + 1} 返回 null");
                    return null;
                }
                data.version = fromVersion + 1;
            }

            if (startVersion != CURRENT_VERSION)
                Debug.Log($"[SaveMigration] 存档已迁移: v{startVersion} → v{CURRENT_VERSION}");

            return data;
        }

        // ═══════════════════════════════════════════
        // 示例迁移函数（Phase 5 实现实际迁移）
        // ═══════════════════════════════════════════

        // private static SaveData MigrateV1ToV2(SaveData data)
        // {
        //     // v1→v2: 新增 totalPlayTime 字段
        //     if (data.totalPlayTime <= 0 && !string.IsNullOrEmpty(data.saveDateTime))
        //         data.totalPlayTime = 0f; // 无法从旧数据推算，使用默认值
        //     return data;
        // }
    }
}
