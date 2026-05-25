using System.Collections.Generic;
using UnityEngine;

namespace _Game.Systems.WorldGen
{
    public enum PreloadStage
    {
        Stage0_ActivateGameObjects,
        Stage1_RebuildContainers,
        Stage2_RespawnNPCs,
        Done
    }

    public class RuntimeChunk
    {
        public int chunkId;
        public Vector2Int gridPos;
        public ChunkState state;
        public PreloadStage preloadStage;
        public Transform chunkParent;

        public readonly List<int> containerIds = new List<int>();
        public readonly List<int> zombieIds = new List<int>();
        public readonly List<Zombie.ZombieStateMachine> zombieInstances = new List<Zombie.ZombieStateMachine>();
        public readonly List<int> buildingIds = new List<int>();
        public readonly List<int> worldItemIds = new List<int>();
    }
}
