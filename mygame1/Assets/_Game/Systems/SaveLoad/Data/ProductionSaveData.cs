using System;

namespace _Game.Systems.SaveLoad
{
    [Serializable]
    public class ProductionSaveData : ICloneable
    {
        public string guid;
        public string deviceName;
        public float fuelRemaining;
        public int inputInstanceId;
        public string inputItemName;
        public int inputCount;
        public int outputInstanceId;
        public string outputItemName;
        public int outputCount;
        public float cycleTimer;            // 当前生产周期已过时间（秒）
        public string outputDestinationGuid;
        public float deviceDurability;       // 前置G：设备磨损耐久（0=未初始化/满耐久）

        public object Clone()
        {
            return new ProductionSaveData
            {
                guid = this.guid, deviceName = this.deviceName, fuelRemaining = this.fuelRemaining,
                cycleTimer = this.cycleTimer,
                inputInstanceId = this.inputInstanceId, inputItemName = this.inputItemName, inputCount = this.inputCount,
                outputInstanceId = this.outputInstanceId, outputItemName = this.outputItemName, outputCount = this.outputCount,
                outputDestinationGuid = this.outputDestinationGuid,
                deviceDurability = this.deviceDurability,
            };
        }
    }
}
