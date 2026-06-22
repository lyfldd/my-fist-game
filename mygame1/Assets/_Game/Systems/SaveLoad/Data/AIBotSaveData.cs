using System;
using System.Collections.Generic;
using System.Linq;

namespace _Game.Systems.SaveLoad
{
    [Serializable]
    public class AIBotSaveData : ICloneable
    {
        public string guid;
        public string buildableName;       // BuildableData.displayName，用于恢复时重建建筑
        public float posX, posY, posZ;
        public float rotY;
        public float hp, battery, uranium;
        public float batteryMax, uraniumMax;
        public string command;
        public string energyMode;
        public bool ecoModeEnabled, burstModeEnabled;
        public float shieldCurrentHP, shieldMaxHP, shieldStartupTimer;
        public bool shieldActive;
        public float followDistance;
        public float speedSliderValue;
        public float guardX, guardY, guardZ;
        public bool hasGuardPosition;
        public float patrolRadius;
        public bool patrolAutoRecallEnabled, patrolAroundPlayer;
        public float patrolCenterX, patrolCenterY, patrolCenterZ;
        public string rightArm, leftArm;
        public List<FusionCoreSaveData> fusionCores;
        public List<SlotSaveData> inventorySlots;

        public object Clone()
        {
            return new AIBotSaveData
            {
                guid = this.guid, buildableName = this.buildableName,
                posX = this.posX, posY = this.posY, posZ = this.posZ,
                rotY = this.rotY,
                hp = this.hp, battery = this.battery, uranium = this.uranium,
                batteryMax = this.batteryMax, uraniumMax = this.uraniumMax,
                command = this.command, energyMode = this.energyMode,
                ecoModeEnabled = this.ecoModeEnabled, burstModeEnabled = this.burstModeEnabled,
                shieldCurrentHP = this.shieldCurrentHP, shieldMaxHP = this.shieldMaxHP,
                shieldStartupTimer = this.shieldStartupTimer, shieldActive = this.shieldActive,
                followDistance = this.followDistance, speedSliderValue = this.speedSliderValue,
                guardX = this.guardX, guardY = this.guardY, guardZ = this.guardZ,
                hasGuardPosition = this.hasGuardPosition,
                patrolRadius = this.patrolRadius,
                patrolAutoRecallEnabled = this.patrolAutoRecallEnabled,
                patrolAroundPlayer = this.patrolAroundPlayer,
                patrolCenterX = this.patrolCenterX, patrolCenterY = this.patrolCenterY, patrolCenterZ = this.patrolCenterZ,
                rightArm = this.rightArm, leftArm = this.leftArm,
                fusionCores = this.fusionCores?.Select(f => f.Clone() as FusionCoreSaveData).ToList(),
                inventorySlots = this.inventorySlots?.Select(s => s.Clone() as SlotSaveData).ToList(),
            };
        }
    }

    [Serializable]
    public class FusionCoreSaveData : ICloneable
    {
        public string itemName;
        public float burnTime;
        public float burnRemaining;
        public float outputRate;

        public object Clone()
        {
            return new FusionCoreSaveData
            {
                itemName = this.itemName, burnTime = this.burnTime,
                burnRemaining = this.burnRemaining, outputRate = this.outputRate,
            };
        }
    }
}
