using System;
using System.Collections.Generic;
using System.Linq;

namespace _Game.Systems.SaveLoad
{
    /// <summary>
    /// 世界实体存档数据：建筑、地面物品、容器状态、阵营变更。
    /// </summary>
    [Serializable]
    public class WorldSaveData : ICloneable
    {
        public List<PlacedStructureSaveData> buildings;
        public List<WorldItemSaveData> groundItems;
        public List<ContainerSaveRecord> containers;
        public List<FactionDeltaSaveData> factionDeltas;

        public object Clone()
        {
            return new WorldSaveData
            {
                buildings = CloneList(this.buildings),
                groundItems = CloneList(this.groundItems),
                containers = CloneList(this.containers),
                factionDeltas = CloneList(this.factionDeltas),
            };
        }

        private static List<T> CloneList<T>(List<T> source) where T : ICloneable
        {
            if (source == null) return null;
            var result = new List<T>(source.Count);
            foreach (var item in source)
                result.Add((T)item?.Clone());
            return result;
        }
    }

    [Serializable]
    public class PlacedStructureSaveData : ICloneable
    {
        public string guid;
        public string buildableName;       // BuildableData.displayName
        public float posX, posY, posZ;
        public float rotY;
        public float currentHealth;

        public object Clone()
        {
            return new PlacedStructureSaveData
            {
                guid = this.guid, buildableName = this.buildableName,
                posX = this.posX, posY = this.posY, posZ = this.posZ,
                rotY = this.rotY, currentHealth = this.currentHealth,
            };
        }
    }

    [Serializable]
    public class WorldItemSaveData : ICloneable
    {
        public int instanceId;             // WorldItem 递增 ID
        public string itemName;
        public int count;
        public float posX, posY, posZ;

        public object Clone()
        {
            return new WorldItemSaveData
            {
                instanceId = this.instanceId, itemName = this.itemName,
                count = this.count, posX = this.posX, posY = this.posY, posZ = this.posZ,
            };
        }
    }

    [Serializable]
    public class ContainerSaveRecord : ICloneable
    {
        public int containerId;
        public bool isOpened;
        public float openedGameDay;
        public string profileName;         // ContainerLootProfile 名称
        public List<SlotSaveData> items;   // null=未生成;空列表=取空;有元素=恢复

        public object Clone()
        {
            return new ContainerSaveRecord
            {
                containerId = this.containerId,
                isOpened = this.isOpened,
                openedGameDay = this.openedGameDay,
                profileName = this.profileName,
                items = this.items?.Select(s => s.Clone() as SlotSaveData).ToList(),
            };
        }
    }

    [Serializable]
    public class FactionDeltaSaveData : ICloneable
    {
        public string factionA;
        public string factionB;
        public string relation;            // "Ally"/"Hostile"/"Neutral"

        public object Clone()
        {
            return new FactionDeltaSaveData { factionA = this.factionA, factionB = this.factionB, relation = this.relation };
        }
    }
}
