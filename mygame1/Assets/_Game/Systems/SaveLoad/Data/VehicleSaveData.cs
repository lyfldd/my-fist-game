using System;

namespace _Game.Systems.SaveLoad
{
    [Serializable]
    public class VehicleSaveData : ICloneable
    {
        public string guid;
        public string vehicleName;
        public float posX, posY, posZ;
        public float rotY;
        public float currentFuel;

        public object Clone()
        {
            return new VehicleSaveData
            {
                guid = this.guid, vehicleName = this.vehicleName,
                posX = this.posX, posY = this.posY, posZ = this.posZ,
                rotY = this.rotY, currentFuel = this.currentFuel,
            };
        }
    }
}
