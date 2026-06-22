using System.Collections.Generic;
using _Game.Systems.Building;
using UnityEngine;

namespace _Game.Systems.SaveLoad
{
    /// <summary>
    /// 已放置建筑注册表。
    /// 维护所有 PlacedStructure 的引用列表，供存档系统遍历（替代 FindObjectsOfType）。
    ///
    /// 挂在 Managers GameObject 上。
    /// </summary>
    public class PlacedStructureRegistry : MonoBehaviour
    {
        public static PlacedStructureRegistry Instance { get; private set; }

        private readonly List<PlacedStructure> _structures = new List<PlacedStructure>();

        public IReadOnlyList<PlacedStructure> AllStructures => _structures;
        public int Count => _structures.Count;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void Register(PlacedStructure structure)
        {
            if (structure != null && !_structures.Contains(structure))
                _structures.Add(structure);
        }

        public void Unregister(PlacedStructure structure)
        {
            _structures.Remove(structure);
        }

        public void Clear()
        {
            _structures.Clear();
        }
    }
}
