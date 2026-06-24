using UnityEditor;
using UnityEngine;
using _Game.Config;

namespace _Game.Editor
{
    /// <summary>
    /// 一键创建新武器/弹药/装备的 RecipeData（工作站配方）。
    /// 工业设备配方（武器组装台/弹药装填机/冲压机/火药厂）通过 ProductionDeviceData 管理，此处跳过。
    /// Tools → 武器装备 → 创建武器装备配方
    /// </summary>
    public class CreateWeaponEquipmentRecipes : EditorWindow
    {
        const string RecipeBase = "Assets/_Game/Config/Recipes";

        [MenuItem("Tools/武器装备/创建武器装备配方")]
        public static void Run()
        {
            int created = 0;

            created += CreateFirearmRecipes();
            created += CreateAmmoComponentRecipes();
            created += CreateEquipmentRecipes();
            created += CreateMaterialRecipes();
            created += CreateSpecialWeaponRecipes();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[配方] 武器装备配方完成，创建/更新 {created} 个配方");
        }

        static void EnsureDir(string dir)
        {
            var parts = dir.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string parent = current;
                current += "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(current))
                    AssetDatabase.CreateFolder(parent, parts[i]);
            }
        }

        static ItemData GetItem(string name)
        {
            var guids = AssetDatabase.FindAssets($"t:ItemData {name}");
            foreach (var g in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                var item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
                if (item != null && item.itemName == name) return item;
            }
            return null;
        }

        static (ItemData, int) M(string name, int count = 1)
        {
            var item = GetItem(name);
            if (item == null) Debug.LogWarning($"[配方] 物品缺失: {name}");
            return (item, count);
        }

        static ItemRequirement[] ToItemReq((ItemData item, int count)[] mats)
        {
            if (mats == null || mats.Length == 0) return new ItemRequirement[0];
            var arr = new ItemRequirement[mats.Length];
            for (int i = 0; i < mats.Length; i++)
                arr[i] = new ItemRequirement { itemData = mats[i].item, count = mats[i].count };
            return arr;
        }

        static RecipeData CreateOrUpdate(string stationDir, string recipeName,
            RecipeCategory category, ItemData result, int resultCount,
            float craftTime, float xp, (ItemData, int)[] materials)
        {
            string dir = $"{RecipeBase}/{stationDir}";
            EnsureDir(dir);
            string path = $"{dir}/{recipeName}.asset";

            var existing = AssetDatabase.LoadAssetAtPath<RecipeData>(path);
            if (existing != null)
            {
                existing.recipeName = recipeName;
                existing.recipeId = recipeName;
                existing.category = category;
                existing.resultItem = result;
                existing.resultCount = resultCount;
                existing.craftTime = craftTime;
                existing.xpReward = xp;
                existing.materials = ToItemReq(materials);
                EditorUtility.SetDirty(existing);
                Debug.Log($"  更新配方: {recipeName} ({stationDir})");
                return existing;
            }

            var recipe = ScriptableObject.CreateInstance<RecipeData>();
            recipe.recipeName = recipeName;
            recipe.recipeId = recipeName;
            recipe.category = category;
            recipe.resultItem = result;
            recipe.resultCount = resultCount;
            recipe.craftTime = craftTime;
            recipe.xpReward = xp;
            recipe.materials = ToItemReq(materials);

            AssetDatabase.CreateAsset(recipe, path);
            Debug.Log($"  创建配方: {recipeName} ({stationDir})");
            return recipe;
        }

        // ===== 火器（工作站手搓）=====
        static int CreateFirearmRecipes()
        {
            int c = 0;

            // M1911 — 高级台
            var m1911 = GetItem("M1911 手枪");
            if (m1911 != null)
            {
                CreateOrUpdate("高级工作台", "M1911手枪", RecipeCategory.Weapon, m1911, 1,
                    8f, 40f, new[] { M("钢锭", 2), M("通用零件", 1) });
                c++;
            }

            // M9 — 高级台
            var m9 = GetItem("M9 手枪");
            if (m9 != null)
            {
                CreateOrUpdate("高级工作台", "M9手枪", RecipeCategory.Weapon, m9, 1,
                    8f, 40f, new[] { M("钢锭", 2), M("通用零件", 1) });
                c++;
            }

            // SKS — 高级台
            var sks = GetItem("SKS 半自动步枪");
            if (sks != null)
            {
                CreateOrUpdate("高级工作台", "SKS半自动步枪", RecipeCategory.Weapon, sks, 1,
                    12f, 50f, new[] { M("钢锭", 3), M("通用零件", 2), M("木板", 2) });
                c++;
            }

            return c;
        }

        // ===== 弹药组件（工作站手搓）=====
        static int CreateAmmoComponentRecipes()
        {
            int c = 0;
            var primer = GetItem("底火");
            if (primer != null)
            {
                CreateOrUpdate("高级工作台", "底火", RecipeCategory.Ammo, primer, 4,
                    3f, 10f, new[] { M("化学试剂", 1), M("铜锭", 1) });
                c++;
            }
            return c;
        }

        // ===== 装备（手工制作 = 非搜刮）=====
        static int CreateEquipmentRecipes()
        {
            int c = 0;

            // --- 简易台 ---
            var motorHelmet = GetItem("摩托车头盔");
            if (motorHelmet != null)
            {
                CreateOrUpdate("简易工作台", "摩托车头盔", RecipeCategory.Armor, motorHelmet, 1,
                    5f, 15f, new[] { M("皮革", 2), M("布匹", 1) });
                c++;
            }

            var leatherJacket = GetItem("皮夹克");
            if (leatherJacket != null)
            {
                CreateOrUpdate("简易工作台", "皮夹克", RecipeCategory.Armor, leatherJacket, 1,
                    6f, 20f, new[] { M("皮革", 3), M("线", 2) });
                c++;
            }

            var cargoPants = GetItem("工装裤");
            if (cargoPants != null)
            {
                CreateOrUpdate("简易工作台", "工装裤", RecipeCategory.Armor, cargoPants, 1,
                    5f, 15f, new[] { M("布匹", 3) });
                c++;
            }

            var lightVest = GetItem("轻背心");
            if (lightVest != null)
            {
                CreateOrUpdate("简易工作台", "轻背心", RecipeCategory.Armor, lightVest, 1,
                    4f, 15f, new[] { M("布匹", 2), M("线", 1) });
                c++;
            }

            var toolBelt = GetItem("工具腰带");
            if (toolBelt != null)
            {
                CreateOrUpdate("简易工作台", "工具腰带", RecipeCategory.Armor, toolBelt, 1,
                    3f, 10f, new[] { M("皮革", 1), M("线", 1) });
                c++;
            }

            var workBoots = GetItem("工装靴");
            if (workBoots != null)
            {
                CreateOrUpdate("简易工作台", "工装靴", RecipeCategory.Armor, workBoots, 1,
                    4f, 15f, new[] { M("皮革", 2) });
                c++;
            }

            // --- 高级台 ---
            var combatHelmet = GetItem("战斗头盔");
            if (combatHelmet != null)
            {
                CreateOrUpdate("高级工作台", "战斗头盔", RecipeCategory.Armor, combatHelmet, 1,
                    6f, 25f, new[] { M("钢锭", 2), M("布匹", 1) });
                c++;
            }

            var gasMask = GetItem("防毒面具");
            if (gasMask != null)
            {
                CreateOrUpdate("高级工作台", "防毒面具", RecipeCategory.Armor, gasMask, 1,
                    8f, 30f, new[] { M("橡胶", 2), M("玻璃板", 1) });
                c++;
            }

            var tacticalJacket = GetItem("战术夹克");
            if (tacticalJacket != null)
            {
                CreateOrUpdate("高级工作台", "战术夹克", RecipeCategory.Armor, tacticalJacket, 1,
                    8f, 30f, new[] { M("布匹", 4), M("尼龙", 2) });
                c++;
            }

            var bulletproofVest = GetItem("防弹衣");
            if (bulletproofVest != null)
            {
                CreateOrUpdate("高级工作台", "防弹衣", RecipeCategory.Armor, bulletproofVest, 1,
                    12f, 50f, new[] { M("凯夫拉", 4), M("尼龙", 2) });
                c++;
            }

            var tacticalPants = GetItem("战术裤");
            if (tacticalPants != null)
            {
                CreateOrUpdate("高级工作台", "战术裤", RecipeCategory.Armor, tacticalPants, 1,
                    6f, 25f, new[] { M("布匹", 3), M("尼龙", 1) });
                c++;
            }

            var tacticalVest = GetItem("战术背心");
            if (tacticalVest != null)
            {
                CreateOrUpdate("高级工作台", "战术背心", RecipeCategory.Armor, tacticalVest, 1,
                    8f, 30f, new[] { M("布匹", 3), M("尼龙", 3) });
                c++;
            }

            var tacticalBeltEquip = GetItem("战术腰带");
            if (tacticalBeltEquip != null)
            {
                CreateOrUpdate("高级工作台", "战术腰带", RecipeCategory.Armor, tacticalBeltEquip, 1,
                    5f, 20f, new[] { M("尼龙", 2), M("通用零件", 1) });
                c++;
            }

            var militaryBag = GetItem("军用背包");
            if (militaryBag != null)
            {
                CreateOrUpdate("高级工作台", "军用背包", RecipeCategory.Armor, militaryBag, 1,
                    8f, 30f, new[] { M("布匹", 4), M("尼龙", 4) });
                c++;
            }

            var combatBoots = GetItem("军靴");
            if (combatBoots != null)
            {
                CreateOrUpdate("高级工作台", "军靴", RecipeCategory.Armor, combatBoots, 1,
                    6f, 25f, new[] { M("皮革", 2), M("橡胶", 1) });
                c++;
            }

            return c;
        }

        // ===== 新材料配方 =====
        static int CreateMaterialRecipes()
        {
            int c = 0;

            var nylon = GetItem("尼龙");
            if (nylon != null)
            {
                CreateOrUpdate("高级工作台", "尼龙", RecipeCategory.Material, nylon, 2,
                    4f, 10f, new[] { M("植物纤维", 3) });
                c++;
            }

            var kevlar = GetItem("凯夫拉");
            if (kevlar != null)
            {
                CreateOrUpdate("研究中心", "凯夫拉", RecipeCategory.Material, kevlar, 2,
                    8f, 25f, new[] { M("化学试剂", 2), M("尼龙", 2) });
                c++;
            }

            var opticalLens = GetItem("光学透镜");
            if (opticalLens != null)
            {
                CreateOrUpdate("电子装配台", "光学透镜", RecipeCategory.Material, opticalLens, 1,
                    6f, 20f, new[] { M("玻璃板", 1), M("铜锭", 1) });
                c++;
            }

            // 陶瓷板 — 工业炉（属于工业设备，暂无工作站配方）
            var ceramic = GetItem("陶瓷板");
            if (ceramic != null)
            {
                Debug.LogWarning("[配方] 陶瓷板需通过工业炉设备生产（非工作站配方）");
            }

            return c;
        }

        // ===== 特殊武器 =====
        static int CreateSpecialWeaponRecipes()
        {
            int c = 0;

            var compoundBow = GetItem("复合弓");
            if (compoundBow != null)
            {
                CreateOrUpdate("高级工作台", "复合弓", RecipeCategory.Weapon, compoundBow, 1,
                    10f, 35f, new[] { M("碳纤维", 1), M("线", 3) });
                c++;
            }

            // 十字弓 — 如果存在则更新
            var crossbow = GetItem("十字弓");
            if (crossbow != null)
            {
                CreateOrUpdate("高级工作台", "十字弓", RecipeCategory.Weapon, crossbow, 1,
                    10f, 35f, new[] { M("钢锭", 2), M("木板", 2), M("线", 2) });
                c++;
            }

            // 电磁步枪
            var emRifle = GetItem("电磁步枪");
            if (emRifle != null)
            {
                CreateOrUpdate("电子装配台", "电磁步枪", RecipeCategory.Weapon, emRifle, 1,
                    20f, 80f, new[] { M("钛合金", 2), M("线圈", 2), M("电容组", 1), M("电路板", 1) });
                c++;
            }

            // 榴弹发射器
            var grenadeLauncher = GetItem("榴弹发射器");
            if (grenadeLauncher != null)
            {
                CreateOrUpdate("高级工作台", "榴弹发射器", RecipeCategory.Weapon, grenadeLauncher, 1,
                    15f, 60f, new[] { M("钢锭", 3), M("高级零件", 2) });
                c++;
            }

            return c;
        }
    }
}
