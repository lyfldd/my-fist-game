using System.Collections.Generic;
using _Game.Systems.WorldGen;
using UnityEngine;

namespace _Game.Systems.WorldContainer
{
    /// <summary>
    /// 容器刷新处理器 — 实现 IRefreshHandler，
    /// Chunk 加载时按冷却天数重置已搜容器。
    /// </summary>
    public class ContainerRefreshHandler : IRefreshHandler
    {
        public string handlerName => "ContainerRefresh";

        public void OnChunkLoad(int chunkId, float currentGameDay)
        {
            var records = ContainerRegistry.Instance?.GetChunkRecords(chunkId);
            if (records == null) return;

            for (int i = 0; i < records.Count; i++)
            {
                var record = records[i];
                if (record == null || record.profile == null) continue;

                // 未搜过 → 懒加载，不创建
                if (!record.isOpened) continue;

                // 不允许刷新 → 保留玩家物品，重建容器
                if (!record.profile.refreshEnabled)
                {
                    continue;
                }

                // 冷却已过 → 清空重置
                float daysSinceOpened = currentGameDay - record.openedGameDay;
                if (record.profile.refreshCooldownDays > 0f
                    && daysSinceOpened >= record.profile.refreshCooldownDays)
                {
                    record.container = null;
                    record.isOpened = false;
                    record.openedGameDay = 0;
                }
                // else: 冷却未到 → 保持 opened，不重建（懒加载在交互时触发）
            }
        }

        public void OnChunkUnload(int chunkId)
        {
            var records = ContainerRegistry.Instance?.GetChunkRecords(chunkId);
            if (records == null) return;

            for (int i = 0; i < records.Count; i++)
            {
                // 释放 InventoryContainer 内存，保留 ContainerRecord
                records[i].container = null;
            }
        }
    }
}
