namespace _Game.Systems.WorldGen.Stages
{
    /// <summary>
    /// Stage 5: 车辆点位生成（Phase 3）。
    /// </summary>
    public class VehicleStage : IGenStage
    {
        public int Order => 50;  // 原45，与 SettlementBuildingStage 不冲突（BuildingStage 用45）
        public bool Enabled => false;

        public void Execute(WorldData data)
        {
            // Phase 3 实现
        }
    }
}
