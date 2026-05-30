using System;
using UnityEngine;

namespace _Game.Config
{
    /// <summary>
    /// 研究阶段：前期大类解锁 → 中期大类解锁 → 后期逐个配方 → 终局逐个配方
    /// </summary>
    public enum ResearchTier
    {
        Early,      // 前期 智力1-3  大类→解锁设备
        Mid,        // 中期 智力4-6  大类→解锁设备
        Late,       // 后期 智力7-8  逐个→解锁配方
        Endgame     // 终局 智力9-10 逐个→解锁配方
    }

    [Serializable]
    public struct ChemicalResearchProject
    {
        public string researchId;
        public string displayName;
        [TextArea(2, 3)]
        public string description;
        public ItemRequirement[] cost;
        [Tooltip("解锁的工业设备名称（对应 ProductionDeviceData.deviceName）。前期/中期用")]
        public string[] unlockedDeviceNames;
        [Tooltip("解锁的配方 ID（对应 RecipeData.recipeId）。后期/终局用")]
        public string[] unlockedRecipeIds;
        public int requiredIntellectLevel;
        public ResearchTier tier;
    }

    [CreateAssetMenu(menuName = "Game/Chemical Research Data")]
    public class ChemicalResearchData : ScriptableObject
    {
        public ChemicalResearchProject[] projects;
    }
}
