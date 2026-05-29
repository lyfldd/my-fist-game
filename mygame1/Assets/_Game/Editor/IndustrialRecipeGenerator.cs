using System.Collections.Generic;
using _Game.Config;
using UnityEditor;
using UnityEngine;

namespace _Game.Editor
{
    /// <summary>
    /// 工业配方生成器 — 从 ProductionDeviceData 提取 ProductionRecipe，
    /// 转换为标准 RecipeData .asset，纳入图谱上下游计算。
    /// </summary>
    public class IndustrialRecipeGenerator : EditorWindow
    {
        [MenuItem("Tools/物品图谱/生成工业 RecipeData")]
        public static void Generate()
        {
            const string industrialDir = "Assets/_Game/Config/Recipes/工业";

            // 确保目录存在
            if (!AssetDatabase.IsValidFolder(industrialDir))
                AssetDatabase.CreateFolder("Assets/_Game/Config/Recipes", "工业");

            // 加载所有生产设备
            var deviceGuids = AssetDatabase.FindAssets("t:ProductionDeviceData");
            var devices = new List<ProductionDeviceData>();
            foreach (var guid in deviceGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var device = AssetDatabase.LoadAssetAtPath<ProductionDeviceData>(path);
                if (device != null) devices.Add(device);
            }

            Debug.Log($"[IndustrialRecipeGenerator] 找到 {devices.Count} 个生产设备");

            int created = 0, skipped = 0;

            foreach (var device in devices)
            {
                if (device.recipes == null || device.recipes.Length == 0)
                    continue;

                // 每个设备一个子目录
                var deviceDir = $"{industrialDir}/{device.deviceName}";
                if (!AssetDatabase.IsValidFolder(deviceDir))
                    AssetDatabase.CreateFolder(industrialDir, device.deviceName);

                foreach (var prodRecipe in device.recipes)
                {
                    if (prodRecipe.output == null)
                    {
                        Debug.LogWarning($"[IndustrialRecipeGenerator] 跳过 {device.deviceName} 中产出为空的配方");
                        skipped++;
                        continue;
                    }

                    // 构建材料列表（兼容单材料和多材料）
                    var materials = BuildMaterials(prodRecipe);
                    if (materials == null || materials.Length == 0)
                    {
                        Debug.LogWarning($"[IndustrialRecipeGenerator] 跳过 {device.deviceName} 中有空引用的配方");
                        skipped++;
                        continue;
                    }

                    var outputName = prodRecipe.output.itemName;

                    // 配方名：设备_产出
                    var recipeName = $"{device.deviceName}_{outputName}";
                    var assetPath = $"{deviceDir}/{recipeName}.asset";

                    // 检查是否已存在
                    var existing = AssetDatabase.LoadAssetAtPath<RecipeData>(assetPath);
                    if (existing != null)
                    {
                        // 更新已有
                        UpdateRecipe(existing, device, prodRecipe, materials);
                        skipped++;
                        continue;
                    }

                    // 新建
                    var recipe = ScriptableObject.CreateInstance<RecipeData>();
                    recipe.recipeName = recipeName;
                    recipe.category = RecipeCategory.Industry;
                    recipe.requiredStation = device.tier;
                    recipe.isIndustrial = true;
                    recipe.productionDeviceName = device.deviceName;
                    recipe.materials = materials;
                    recipe.resultItem = prodRecipe.output;
                    recipe.resultCount = prodRecipe.outputCount;
                    recipe.craftTime = prodRecipe.baseTime;
                    recipe.xpReward = 0f;

                    var matDesc = "";
                    for (int i = 0; i < materials.Length; i++)
                    {
                        if (i > 0) matDesc += "+";
                        var n = materials[i].itemData != null ? materials[i].itemData.itemName : "?";
                        matDesc += $"{n}×{materials[i].count}";
                    }
                    recipe.description = $"{device.deviceName}自动生产：{matDesc} → {outputName} x{prodRecipe.outputCount}";

                    AssetDatabase.CreateAsset(recipe, assetPath);
                    created++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[IndustrialRecipeGenerator] 完成！新建 {created}，跳过/更新 {skipped}");
            EditorUtility.DisplayDialog("工业 RecipeData 生成完成",
                $"新建: {created} 个\n跳过/更新: {skipped} 个\n\n保存至: {industrialDir}\n\n请运行「构建 ItemGraph」重新计算上下游关系。",
                "确定");
        }

        static ItemRequirement[] BuildMaterials(ProductionRecipe prodRecipe)
        {
            // 多材料输入优先
            if (prodRecipe.inputs != null && prodRecipe.inputs.Length > 0)
            {
                var valid = new List<ItemRequirement>();
                foreach (var req in prodRecipe.inputs)
                    if (req.itemData != null)
                        valid.Add(req);
                return valid.ToArray();
            }

            // 单材料输入（兼容旧数据）
            if (prodRecipe.input != null)
            {
                return new ItemRequirement[]
                {
                    new ItemRequirement { itemData = prodRecipe.input, count = prodRecipe.inputCount }
                };
            }

            return null;
        }

        static void UpdateRecipe(RecipeData recipe, ProductionDeviceData device, ProductionRecipe prodRecipe, ItemRequirement[] materials)
        {
            recipe.requiredStation = device.tier;
            recipe.isIndustrial = true;
            recipe.productionDeviceName = device.deviceName;
            recipe.materials = materials;
            recipe.resultItem = prodRecipe.output;
            recipe.resultCount = prodRecipe.outputCount;
            recipe.craftTime = prodRecipe.baseTime;
            recipe.xpReward = 0f;
            EditorUtility.SetDirty(recipe);
        }
    }
}
