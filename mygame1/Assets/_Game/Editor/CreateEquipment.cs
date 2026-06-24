using UnityEditor;
using UnityEngine;
using _Game.Config;

namespace _Game.Editor
{
    /// <summary>
    /// 一键创建 T0-T3 装备 + 新材料。
    /// Tools → 武器装备 → 创建全部装备
    /// </summary>
    public class CreateEquipment : EditorWindow
    {
        [MenuItem("Tools/武器装备/创建全部装备")]
        public static void Run()
        {
            string baseDir = "Assets/_Game/Config/Items";
            int created = 0;

            created += CreateMaterials(baseDir);
            created += CreateT0Scavenge(baseDir);
            created += CreateT1SimpleBench(baseDir);
            created += CreateT2AdvancedBench(baseDir);
            created += CreateT3EndGame(baseDir);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[装备] 全部完成，创建 {created} 个物品");
        }

        static ItemData Create(string dir, string file, string name, ItemCategory cat, int w, int h, float weight, float armor, float warmth, int storageW = 1, int storageH = 1, EquipSlot slot = EquipSlot.None, int maxStack = 1)
        {
            EnsureDir(dir);
            var so = ScriptableObject.CreateInstance<ItemData>();
            so.itemName = name; so.category = cat;
            so.gridWidth = w; so.gridHeight = h; so.weight = weight; so.maxStack = maxStack;
            so.armorValue = armor; so.warmthValue = warmth;
            so.equipSlot = slot;
            so.storageWidth = storageW; so.storageHeight = storageH;
            so.hasDurability = true; so.maxDurability = 50;
            string path = $"{dir}/{file}.asset";
            AssetDatabase.CreateAsset(so, path);
            Debug.Log($"  创建: {name}");
            return so;
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

        // 新材料
        static int CreateMaterials(string baseDir)
        {
            string dir = baseDir + "/Materials";
            Create(dir, "Nylon", "尼龙", ItemCategory.SemiFinished, 1, 1, 0.1f, 0, 0, maxStack: 20);
            Create(dir, "Kevlar", "凯夫拉", ItemCategory.SemiFinished, 1, 1, 0.2f, 0, 0, maxStack: 10);
            Create(dir, "CeramicPlate", "陶瓷板", ItemCategory.SemiFinished, 1, 2, 0.5f, 0, 0, maxStack: 5);
            Create(dir, "OpticalLens", "光学透镜", ItemCategory.SemiFinished, 1, 1, 0.1f, 0, 0, maxStack: 5);
            Create(dir, "Shoelace", "鞋带", ItemCategory.SemiFinished, 1, 1, 0.05f, 0, 0, maxStack: 10);
            Create(dir, "RubberSole", "橡胶鞋底", ItemCategory.SemiFinished, 1, 1, 0.1f, 0, 0, maxStack: 10);
            return 6;
        }

        // T0 搜刮
        static int CreateT0Scavenge(string baseDir)
        {
            string dir = baseDir + "/Equipment/T0_Scavenge";
            Create(dir, "TShirt", "T恤", ItemCategory.Equipment, 1, 1, 0.3f, 0, 0.5f, 1, 1, EquipSlot.Tops);
            Create(dir, "Jeans", "牛仔裤", ItemCategory.Equipment, 1, 1, 0.5f, 0, 0.5f, 1, 1, EquipSlot.Pants);
            Create(dir, "BaseballCap", "棒球帽", ItemCategory.Equipment, 1, 1, 0.2f, 0, 0.5f, 1, 1, EquipSlot.Head);
            Create(dir, "Sneakers", "运动鞋", ItemCategory.Equipment, 1, 1, 0.3f, 0, 0.5f, 1, 1, EquipSlot.None);
            Create(dir, "SmallBag", "小背包", ItemCategory.Equipment, 1, 1, 0.3f, 0, 0, 3, 3, EquipSlot.Backpack);
            Create(dir, "LeatherBelt", "皮带", ItemCategory.Equipment, 1, 1, 0.2f, 0, 0, 1, 1, EquipSlot.Belt);
            return 6;
        }

        // T1 简易台
        static int CreateT1SimpleBench(string baseDir)
        {
            string dir = baseDir + "/Equipment/T1_SimpleBench";
            Create(dir, "LeatherJacket", "皮夹克", ItemCategory.Equipment, 1, 2, 1.5f, 2, 2f, 1, 1, EquipSlot.Tops);
            Create(dir, "CargoPants", "工装裤", ItemCategory.Equipment, 1, 2, 1f, 1, 1f, 2, 2, EquipSlot.Pants);
            Create(dir, "MotorHelmet", "摩托车头盔", ItemCategory.Equipment, 1, 1, 1f, 3, 1f, 1, 1, EquipSlot.Head);
            Create(dir, "WorkBoots", "工装靴", ItemCategory.Equipment, 1, 1, 0.8f, 1, 1f, 1, 1, EquipSlot.None);
            Create(dir, "ToolBelt", "工具腰带", ItemCategory.Equipment, 1, 1, 0.3f, 0, 0, 2, 2, EquipSlot.Belt);
            Create(dir, "LightVest", "轻背心", ItemCategory.Equipment, 1, 1, 0.8f, 2, 0.5f, 2, 2, EquipSlot.Vest);
            return 6;
        }

        // T2 高级台
        static int CreateT2AdvancedBench(string baseDir)
        {
            string dir = baseDir + "/Equipment/T2_AdvancedBench";
            Create(dir, "TacticalJacket", "战术夹克", ItemCategory.Equipment, 1, 2, 1.5f, 5, 1.5f, 2, 2, EquipSlot.Tops);
            Create(dir, "TacticalPants", "战术裤", ItemCategory.Equipment, 1, 2, 1.2f, 4, 1f, 2, 2, EquipSlot.Pants);
            Create(dir, "CombatHelmet", "战斗头盔", ItemCategory.Equipment, 1, 1, 1.2f, 8, 0.5f, 1, 1, EquipSlot.Head);
            Create(dir, "CombatBoots", "军靴", ItemCategory.Equipment, 1, 1, 1f, 3, 1f, 1, 1, EquipSlot.None);
            Create(dir, "TacticalBelt", "战术腰带", ItemCategory.Equipment, 1, 1, 0.5f, 1, 0, 3, 2, EquipSlot.Belt);
            Create(dir, "TacticalVest", "战术背心", ItemCategory.Equipment, 1, 1, 1.5f, 6, 1f, 3, 3, EquipSlot.Vest);
            Create(dir, "MilitaryBag", "军用背包", ItemCategory.Equipment, 1, 1, 1f, 1, 0, 6, 4, EquipSlot.Backpack);
            return 7;
        }

        // T3 终局
        static int CreateT3EndGame(string baseDir)
        {
            string dir = baseDir + "/Equipment/T3_EndGame";
            Create(dir, "BulletproofVest", "防弹衣", ItemCategory.Equipment, 1, 2, 2.5f, 15, 0.5f, 2, 1, EquipSlot.BodyArmor);
            Create(dir, "PlateCarrier", "插板背心", ItemCategory.Equipment, 1, 2, 3f, 12, 0, 2, 2, EquipSlot.Vest);
            Create(dir, "GasMask", "防毒面具", ItemCategory.Equipment, 1, 1, 0.5f, 2, 0, 1, 1, EquipSlot.Head);
            return 3;
        }
    }
}
