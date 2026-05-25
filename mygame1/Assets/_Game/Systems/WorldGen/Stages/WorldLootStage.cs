namespace _Game.Systems.WorldGen.Stages
{
    /// <summary>
    /// Stage 7: 野外随机物资（Phase 3）。
    /// </summary>
    public class WorldLootStage : IGenStage
    {
        public int Order => 60;  // 原55
        public bool Enabled => false;

        public void Execute(WorldData data)
        {
            // Phase 3 实现：泊松圆盘采样撒点
        }
    }
}
