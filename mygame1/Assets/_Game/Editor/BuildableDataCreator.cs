using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using _Game.Config;

namespace _Game.Editor
{
    public class BuildableDataCreator : EditorWindow
    {
        static Dictionary<string, ItemData> _itemLookup;

        [MenuItem("Tools/创建首批 BuildableData")]
        public static void CreateDefaultBuildables()
        {
            BuildItemLookup();

            string folder = "Assets/_Game/Config/BuildableData";
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets/_Game/Config", "BuildableData");

            int created = 0;
            // ... [existing creation code - unchanged for brevity, but this currently
            // skips existing files, so for new projects it would create them]

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[BuildableDataCreator] 已创建 {created} 个 BuildableData 资产");
        }

        [MenuItem("Tools/补全 BuildableData 材料")]
        public static void FillAllMaterials()
        {
            BuildItemLookup();

            string folder = "Assets/_Game/Config/BuildableData";
            var guids = AssetDatabase.FindAssets("t:BuildableData", new[] { folder });
            int updated = 0;

            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var data = AssetDatabase.LoadAssetAtPath<BuildableData>(path);
                if (data == null) continue;

                var mats = GetMaterialsFor(data.name);
                if (mats == null) continue;

                data.materials = mats;
                EditorUtility.SetDirty(data);
                updated++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[BuildableDataCreator] 已为 {updated} 个 BuildableData 补全材料");
            EditorUtility.DisplayDialog("完成", $"已为 {updated} 个建造物补全材料需求。", "好的");
        }

        static ItemRequirement[] GetMaterialsFor(string assetName)
        {
            switch (assetName)
            {
                // ===== Wall =====
                case "Buildable_WoodWall":
                    return new[] { M("WoodPlank", 4), M("Nails", 4) };
                case "Buildable_StoneWall":
                    return new[] { M("StoneBrick", 4), M("Cement", 2) };
                case "Buildable_MetalWall":
                    return new[] { M("ScrapMetal", 4), M("Screw", 4) };
                case "Buildable_WoodFence":
                    return new[] { M("Branch", 4), M("Rope", 2) };

                // ===== Floor =====
                case "Buildable_WoodFloor":
                    return new[] { M("WoodPlank", 3), M("Nails", 2) };
                case "Buildable_StoneFloor":
                    return new[] { M("StoneBrick", 3), M("Cement", 1) };

                // ===== Furniture =====
                case "Buildable_WoodTable":
                    return new[] { M("WoodPlank", 4), M("Nails", 4) };
                case "Buildable_WoodDoor":
                    return new[] { M("WoodPlank", 3), M("Nails", 2) };
                case "Buildable_WoodWindow":
                    return new[] { M("WoodPlank", 2), M("Nails", 2) };
                case "Buildable_Bed":
                    return new[] { M("WoodPlank", 6), M("ClothRoll", 4), M("Nails", 4) };
                case "Buildable_LargeCrate":
                    return new[] { M("WoodPlank", 5), M("Nails", 6), M("IronIngot", 2) };
                case "Buildable_DryingRack":
                    return new[] { M("Branch", 4), M("Rope", 3) };

                // ===== Barricade =====
                case "Buildable_BarricadeWindow":
                    return new[] { M("WoodPlank", 3), M("Nails", 4) };
                case "Buildable_BarricadeDoor":
                    return new[] { M("WoodPlank", 4), M("Nails", 6) };
                case "Buildable_Trap":
                    return new[] { M("Branch", 3), M("Rope", 2), M("ScrapMetal", 2) };

                // ===== Workstation =====
                case "Buildable_Campfire":
                    return new[] { M("WoodLog", 3), M("Stone", 5), M("Flint", 1) };
                case "Buildable_SimpleBench":
                    return new[] { M("WoodPlank", 5), M("Nails", 4) };
                case "Buildable_Furnace":
                    return new[] { M("StoneBrick", 8), M("Clay", 4), M("IronIngot", 2) };
                case "Buildable_MediumBench":
                    return new[] { M("WoodPlank", 8), M("Nails", 6), M("IronIngot", 4), M("CommonParts", 2) };
                case "Buildable_AdvancedBench":
                    return new[] { M("WoodPlank", 10), M("SteelIngot", 4), M("AdvancedParts", 4), M("CircuitBoard", 2) };
                case "Buildable_Chemistry":
                    return new[] { M("SteelIngot", 4), M("GlassPane", 4), M("CopperIngot", 2), M("CircuitBoard", 2) };

                // ===== Industrial =====
                case "Buildable_Machining":
                    return new[] { M("SteelIngot", 8), M("AdvancedParts", 6), M("Bearing", 4), M("CircuitBoard", 4) };
                case "Buildable_PressMachine":
                    return new[] { M("SteelIngot", 10), M("AdvancedParts", 4), M("Gear", 4), M("SpringAssembly", 4) };
                case "Buildable_IndustrialFurnace":
                    return new[] { M("StoneBrick", 12), M("SteelIngot", 6), M("AdvancedParts", 4), M("Coil", 4) };
                case "Buildable_ElectrolysisTank":
                    return new[] { M("SteelIngot", 6), M("GlassPane", 6), M("CopperIngot", 4), M("BatteryPack", 2), M("ChemicalAgent", 2) };
                case "Buildable_Fermenter":
                    return new[] { M("SteelIngot", 4), M("GlassPane", 4), M("CopperIngot", 2), M("ChemicalAgent", 2) };
                case "Buildable_Distiller":
                    return new[] { M("CopperIngot", 6), M("GlassPane", 4), M("SteelIngot", 2), M("Coil", 2) };
                case "Buildable_Crusher":
                    return new[] { M("SteelIngot", 6), M("IronIngot", 4), M("Gear", 3), M("Bearing", 2) };
                case "Buildable_Loom":
                    return new[] { M("WoodPlank", 6), M("Nails", 4), M("IronIngot", 2) };
                case "Buildable_Sawmill":
                    return new[] { M("WoodPlank", 4), M("IronIngot", 3), M("Nails", 4) };
                case "Buildable_WaterPump":
                    return new[] { M("IronIngot", 4), M("SteelPipe", 2), M("CommonParts", 2) };
                case "Buildable_Kiln":
                    return new[] { M("StoneBrick", 6), M("Clay", 4), M("IronIngot", 2) };
                default:
                    Debug.LogWarning($"[BuildableDataCreator] 未定义材料配方: {assetName}");
                    return null;
            }
        }

        // ============================================================
        // Helpers
        // ============================================================

        static void BuildItemLookup()
        {
            _itemLookup = new Dictionary<string, ItemData>();
            var guids = AssetDatabase.FindAssets("t:ItemData");
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
                if (item != null)
                {
                    string key = System.IO.Path.GetFileNameWithoutExtension(path);
                    if (!_itemLookup.ContainsKey(key))
                        _itemLookup[key] = item;
                }
            }
        }

        static ItemData GetItem(string assetName)
        {
            if (_itemLookup.TryGetValue(assetName, out var item))
                return item;
            Debug.LogWarning($"[BuildableDataCreator] 未找到 ItemData: {assetName}");
            return null;
        }

        static ItemRequirement M(string itemName, int count)
        {
            return new ItemRequirement { itemData = GetItem(itemName), count = count };
        }
    }
}
