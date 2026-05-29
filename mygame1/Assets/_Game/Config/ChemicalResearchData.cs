using System;
using UnityEngine;

namespace _Game.Config
{
    [Serializable]
    public struct ChemicalResearchProject
    {
        public string researchId;
        public string displayName;
        [TextArea(2, 3)]
        public string description;
        public ItemRequirement[] cost;
        [Tooltip("解锁的工业设备名称（对应 ProductionDeviceData.deviceName）")]
        public string[] unlockedDeviceNames;
        public int requiredIntellectLevel;
    }

    [CreateAssetMenu(menuName = "Game/Chemical Research Data")]
    public class ChemicalResearchData : ScriptableObject
    {
        public ChemicalResearchProject[] projects;
    }
}
