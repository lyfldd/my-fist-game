using System.Collections.Generic;
using UnityEngine;
using _Game.Config;
using _Game.Core;
using _Game.Systems.Character;

namespace _Game.Systems.Crafting
{
    /// <summary>
    /// 合成系统核心。挂载在场景中的单例 MonoBehaviour。
    /// 负责配方查询、权限校验、扣材料、出成品。
    /// </summary>
    public class CraftingSystem : MonoBehaviour
    {
        public static CraftingSystem Instance { get; private set; }

        [SerializeField] RecipeCatalog _catalog;
        Inventory.Inventory _inventory;
        SurvivalXPSystem _xpSystem;
        StaminaSystem _stamina;

        public WorkstationTier ActiveStation { get; set; } = WorkstationTier.Hands;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (_catalog == null) _catalog = Resources.Load<RecipeCatalog>("RecipeCatalog");
            _inventory = ServiceLocator.Get<Inventory.Inventory>();
            _xpSystem = SurvivalXPSystem.Instance;
            if (_xpSystem == null)
                _xpSystem = ServiceLocator.Get<SurvivalXPSystem>();
            _stamina = ServiceLocator.Get<StaminaSystem>();
        }

        /// <summary>
        /// 获取当前工作台可用的所有配方（过滤技能+材料）。
        /// </summary>
        public List<RecipeData> GetAvailableRecipes()
        {
            var list = new List<RecipeData>();
            if (_catalog == null) return list;
            foreach (var recipe in _catalog.GetByStation(ActiveStation))
            {
                if (CheckSkill(recipe) && HasMaterials(recipe))
                    list.Add(recipe);
            }
            return list;
        }

        /// <summary>
        /// 获取指定工作台的全部配方（不过滤）。
        /// </summary>
        public RecipeData[] GetAllRecipesForStation(WorkstationTier tier)
        {
            if (_catalog == null) return new RecipeData[0];
            return _catalog.GetByStation(tier);
        }

        /// <summary>
        /// 获取当前工作台的全部配方（不过滤技能/材料）。
        /// </summary>
        public RecipeData[] GetAllRecipesForCurrentStation()
        {
            return GetAllRecipesForStation(ActiveStation);
        }

        /// <summary>
        /// 获取当前工作台及所有低级工作台的全部配方（不过滤）。
        /// 高级工作台继承低级内容。
        /// </summary>
        public List<RecipeData> GetAllRecipesForStationAndBelow(WorkstationTier tier)
        {
            var list = new List<RecipeData>();
            if (_catalog == null) return list;
            for (int t = 0; t <= (int)tier; t++)
            {
                var recipes = _catalog.GetByStation((WorkstationTier)t);
                if (recipes != null) list.AddRange(recipes);
            }
            return list;
        }

        /// <summary>
        /// 获取当前工作台及所有低级工作台的全部配方（便捷方法）。
        /// </summary>
        public List<RecipeData> GetAllRecipesForCurrentStationAndBelow()
        {
            return GetAllRecipesForStationAndBelow(ActiveStation);
        }

        /// <summary>
        /// 检查是否可以合成指定配方。
        /// </summary>
        public bool CanCraft(RecipeData recipe)
        {
            if (recipe == null) return false;
            if (recipe.requiredStation > ActiveStation) return false;
            if (!CheckSkill(recipe)) return false;
            if (!HasMaterials(recipe)) return false;
            return true;
        }

        /// <summary>
        /// 执行合成。扣材料 → 扣体力 → 出成品 → XP。
        /// 背包满时回滚材料。
        /// </summary>
        public bool Craft(RecipeData recipe)
        {
            if (!CanCraft(recipe)) return false;

            foreach (var req in recipe.materials)
                _inventory.RemoveItem(req.itemData, req.count);

            ConsumeStamina(recipe);

            int added = _inventory.AddItem(recipe.resultItem, recipe.resultCount);
            if (added <= 0)
            {
                foreach (var req in recipe.materials)
                    _inventory.AddItem(req.itemData, req.count);
                EventBus.Publish(new CraftingFailedEvent(recipe.recipeName, "背包已满"));
                return false;
            }

            EventBus.Publish(new CraftingCompletedEvent(
                recipe.recipeName, recipe.resultItem, recipe.resultCount, recipe.xpReward));

            _xpSystem.AddXP((int)recipe.xpReward);

            return true;
        }

        /// <summary>
        /// 获取指定配方可制作的次数。
        /// </summary>
        public int GetCraftableCount(RecipeData recipe)
        {
            if (recipe == null || recipe.materials == null) return 0;
            int min = int.MaxValue;
            foreach (var req in recipe.materials)
            {
                int count = _inventory.GetItemCount(req.itemData) / req.count;
                if (count < min) min = count;
            }
            return min == int.MaxValue ? 0 : min;
        }

        bool CheckSkill(RecipeData recipe)
        {
            if (recipe.skillRequirements == null || recipe.skillRequirements.Length == 0) return true;
            if (_xpSystem == null) return true; // 无经验系统时不限制
            foreach (var req in recipe.skillRequirements)
            {
                if (_xpSystem.GetSkillLevel(req.skill) < req.level)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 只检查材料（不检查技能），供 UI 区分提示用。
        /// </summary>
        public bool HasMaterialsFor(RecipeData recipe)
        {
            if (recipe == null || recipe.materials == null || recipe.materials.Length == 0) return true;
            if (_inventory == null) return false;
            foreach (var req in recipe.materials)
            {
                if (!_inventory.HasItem(req.itemData, req.count))
                    return false;
            }
            return true;
        }

        bool HasMaterials(RecipeData recipe)
        {
            if (recipe.materials == null || recipe.materials.Length == 0) return true;
            if (_inventory == null) return true; // 无背包时不限制
            foreach (var req in recipe.materials)
            {
                if (!_inventory.HasItem(req.itemData, req.count))
                    return false;
            }
            return true;
        }

        void ConsumeStamina(RecipeData recipe)
        {
            if (_stamina == null) return;
            float cost = recipe.requiredStation switch
            {
                WorkstationTier.Hands or WorkstationTier.Campfire => 10f,
                WorkstationTier.SimpleBench => 5f,
                _ => 0f
            };
            if (cost > 0f)
                _stamina.Consume(cost);
        }
    }
}
