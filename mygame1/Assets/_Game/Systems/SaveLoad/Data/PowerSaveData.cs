using System;
using System.Collections.Generic;
using System.Linq;

namespace _Game.Systems.SaveLoad
{
    [Serializable]
    public class PowerSaveData : ICloneable
    {
        public List<PowerSourceSaveData> sources;
        public List<PowerTerminalSaveData> terminals;
        public List<PowerConsumerSaveData> consumers;

        public object Clone()
        {
            return new PowerSaveData
            {
                sources = CloneList(this.sources),
                terminals = CloneList(this.terminals),
                consumers = CloneList(this.consumers),
            };
        }

        private static List<T> CloneList<T>(List<T> source) where T : ICloneable
        {
            if (source == null) return null;
            return source.Select(item => (T)item?.Clone()).ToList();
        }
    }

    [Serializable]
    public class PowerSourceSaveData : ICloneable
    {
        public string guid;
        public float fuelRemaining, durability;

        public object Clone()
        {
            return new PowerSourceSaveData { guid = this.guid, fuelRemaining = this.fuelRemaining, durability = this.durability };
        }
    }

    [Serializable]
    public class PowerTerminalSaveData : ICloneable
    {
        public string guid;
        public List<string> connectedSourceGuids;
        public List<string> connectedTerminalGuids;

        public object Clone()
        {
            return new PowerTerminalSaveData
            {
                guid = this.guid,
                connectedSourceGuids = this.connectedSourceGuids != null ? new List<string>(this.connectedSourceGuids) : null,
                connectedTerminalGuids = this.connectedTerminalGuids != null ? new List<string>(this.connectedTerminalGuids) : null,
            };
        }
    }

    [Serializable]
    public class PowerConsumerSaveData : ICloneable
    {
        public string guid;
        public bool isManuallyOff;

        public object Clone()
        {
            return new PowerConsumerSaveData { guid = this.guid, isManuallyOff = this.isManuallyOff };
        }
    }
}
