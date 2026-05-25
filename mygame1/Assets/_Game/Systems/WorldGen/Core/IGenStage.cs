namespace _Game.Systems.WorldGen
{
    /// <summary>
    /// 管线 Stage 接口。所有生成步骤实现此接口，由 WorldGenerator 按 Order 调度。
    /// </summary>
    public interface IGenStage
    {
        int Order { get; }
        bool Enabled { get; }
        void Execute(WorldData data);
    }
}
