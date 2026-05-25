namespace _Game.Systems.WorldGen.Stages
{
    /// <summary>
    /// Stage 9: 僵尸刷新区标记。
    /// 实际刷怪由 ChunkManager.Stage2_RespawnNPCs → ZombieSpawner.SpawnInChunk 完成。
    /// 此 Stage 负责在世界生成阶段标记刷怪区（预留），运行时由 ChunkManager 驱动。
    /// </summary>
    public class SpawnStage : IGenStage
    {
        public int Order => 75;
        public bool Enabled => false; // 运行时刷怪由 ChunkManager 驱动

        public void Execute(WorldData data)
        {
            // Phase 3: 根据区域类型预计算各 Chunk 的僵尸密度/类型分布
            // 当前运行时刷怪由 ZombieSpawner 统一处理
        }
    }
}
