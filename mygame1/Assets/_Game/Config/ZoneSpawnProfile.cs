using System;
using UnityEngine;

namespace _Game.Config
{
    /// <summary>
    /// 区块刷怪配置。定义某类地段的僵尸数量、刷新间隔、类型权重。
    /// </summary>
    [CreateAssetMenu(menuName = "Game/ZoneSpawnProfile")]
    public class ZoneSpawnProfile : ScriptableObject
    {
        public string zoneName = "默认";

        [Header("数量")]
        [Range(0, 20)] public int initialMin = 2;
        [Range(0, 20)] public int initialMax = 5;
        [Range(1, 30)] public int maxPerChunk = 10;

        [Header("持续刷新")]
        [Tooltip("两次补刷检查的最小间隔（秒）")]
        public float respawnInterval = 120f;
        [Range(20f, 80f)]
        [Tooltip("刷新点必须距离玩家至少多远")]
        public float minSpawnDistFromPlayer = 25f;
        [Tooltip("每轮补刷最多刷几只")]
        [Range(1, 5)] public int maxPerRespawnBatch = 2;

        [Header("类型权重（权重总和不必为 100，按比例分配）")]
        public ZombieTypeWeight[] typeWeights;

        [Serializable]
        public struct ZombieTypeWeight
        {
            public ZombieData data;
            [Range(0, 100)] public int weight;
        }
    }
}
