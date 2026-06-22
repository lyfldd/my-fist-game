using System;
using System.Collections.Generic;
using UnityEngine;

namespace _Game.Systems.SaveLoad
{
    /// <summary>
    /// 顶层保存数据结构。包含所有子系统数据的树根。
    /// ICloneable 用于主线程深拷贝（~1-2ms），切断与后台线程的引用共享。
    /// </summary>
    [Serializable]
    public class SaveData : ICloneable
    {
        public int version = 1;
        public int worldGenVersion = 1;
        public int worldSeed;
        public string saveDateTime;
        public float totalPlayTime;

        public PlayerSaveData player;
        public InventorySaveData inventory;
        public WorldSaveData world;
        public PowerSaveData power;
        public List<AIBotSaveData> aiBots;
        public List<ProductionSaveData> productions;
        public List<VehicleSaveData> vehicles;
        public List<ZombieSaveData> nearbyZombies;
        public TimeWeatherSaveData timeWeather;
        public ResearchSaveData research;

        public object Clone()
        {
            var clone = new SaveData
            {
                version = this.version,
                worldGenVersion = this.worldGenVersion,
                worldSeed = this.worldSeed,
                saveDateTime = this.saveDateTime,
                totalPlayTime = this.totalPlayTime,
                player = this.player?.Clone() as PlayerSaveData,
                inventory = this.inventory?.Clone() as InventorySaveData,
                world = this.world?.Clone() as WorldSaveData,
                power = this.power?.Clone() as PowerSaveData,
                timeWeather = this.timeWeather?.Clone() as TimeWeatherSaveData,
                research = this.research?.Clone() as ResearchSaveData,
                aiBots = CloneList(this.aiBots),
                productions = CloneList(this.productions),
                vehicles = CloneList(this.vehicles),
                nearbyZombies = CloneList(this.nearbyZombies),
            };
            return clone;
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
}
