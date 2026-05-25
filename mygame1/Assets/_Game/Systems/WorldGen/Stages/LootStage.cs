namespace _Game.Systems.WorldGen.Stages
{
    /// <summary>
    /// Stage 8: 建筑容器 Loot（Phase 3）。
    /// </summary>
    public class LootStage : IGenStage
    {
        public int Order => 65;  // 原60
        public bool Enabled => false;

        public void Execute(WorldData data)
        {
            // Phase 3 实现：容器预埋 + Loot 表绑定
        }
    }
}
