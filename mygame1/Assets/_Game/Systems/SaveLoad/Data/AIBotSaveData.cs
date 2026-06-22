using System;
using System.Collections.Generic;
using System.Linq;

namespace _Game.Systems.SaveLoad
{
    [Serializable]
    public class AIBotSaveData : ICloneable
    {
        public string guid;
        public float posX, posY, posZ;
        public float hp, battery, uranium, shield;
        public string command;
        public float guardX, guardY, guardZ;
        public bool hasGuardPosition;
        public List<float> fusionCores;
        public List<string> weapons;
        public List<SlotSaveData> ammoSlots;
        public List<SlotSaveData> inventorySlots;

        public object Clone()
        {
            return new AIBotSaveData
            {
                guid = this.guid, posX = this.posX, posY = this.posY, posZ = this.posZ,
                hp = this.hp, battery = this.battery, uranium = this.uranium, shield = this.shield,
                command = this.command,
                guardX = this.guardX, guardY = this.guardY, guardZ = this.guardZ,
                hasGuardPosition = this.hasGuardPosition,
                fusionCores = this.fusionCores != null ? new List<float>(this.fusionCores) : null,
                weapons = this.weapons != null ? new List<string>(this.weapons) : null,
                ammoSlots = this.ammoSlots?.Select(s => s.Clone() as SlotSaveData).ToList(),
                inventorySlots = this.inventorySlots?.Select(s => s.Clone() as SlotSaveData).ToList(),
            };
        }
    }
}
