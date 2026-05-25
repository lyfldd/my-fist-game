namespace _Game.Systems.WorldGen.Stages
{
    /// <summary>
    /// Stage 1 (Order=10): 种子初始化。
    /// 使用 System.Random 确定性随机数生成器。
    /// </summary>
    public class SeedStage : IGenStage
    {
        public int Order => 10;
        public bool Enabled => true;

        public void Execute(WorldData data)
        {
            data.rng = new System.Random(data.seed);
            data.random = data.rng; // 向后兼容旧字段
        }
    }
}
