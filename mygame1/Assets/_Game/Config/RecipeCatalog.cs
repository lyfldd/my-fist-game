using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace _Game.Config
{
    /// <summary>
    /// 配方总表 ScriptableObject。
    /// 右键 → Create → Game/Recipe Catalog 创建。
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Recipe Catalog")]
    public class RecipeCatalog : ScriptableObject
    {
        public RecipeData[] recipes;

        public RecipeData[] GetByStation(WorkstationTier tier)
        {
            return recipes.Where(r => r != null && r.requiredStation == tier).ToArray();
        }

        public RecipeData[] GetByCategory(RecipeCategory cat)
        {
            return recipes.Where(r => r != null && r.category == cat).ToArray();
        }
    }
}
