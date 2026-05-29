using System;
using UnityEngine;

namespace _Game.Config
{
    [Serializable]
    public struct SkillRequirement
    {
        public SkillType skill;
        public int level;
    }

    public enum RecipeCategory
    {
        Tool, Building, Weapon, Armor, Consumable, Ammo,
        Vehicle, Material, Cooking, Smelting, Industry,
        Furniture, Farming, Defense
    }

    /// <summary>
    /// 单个配方 ScriptableObject。
    /// 右键 → Create → Game/Recipe 创建。
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Recipe")]
    public class RecipeData : ScriptableObject
    {
        [Header("基础信息")]
        public string recipeName;
        public Sprite icon;
        [TextArea(2, 3)]
        public string description;
        public RecipeCategory category;

        [Header("合成需求")]
        public WorkstationTier requiredStation;
        public bool isIndustrial;
        public string productionDeviceName;
        public SkillRequirement[] skillRequirements;
        public ItemRequirement[] materials;

        [Header("产出")]
        public ItemData resultItem;
        public int resultCount = 1;

        [Header("合成参数")]
        public float craftTime = 2f;
        public float xpReward = 15f;

        [Header("拆解")]
        public bool canDeconstruct;
        [Range(0, 1)]
        public float deconstructReturnRate = 0.5f;
    }
}
