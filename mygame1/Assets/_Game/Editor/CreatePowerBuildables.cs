using UnityEngine;
using UnityEditor;
using _Game.Config;
using _Game.Systems.Power;

namespace _Game.Editor
{
    /// <summary>
    /// 一键生成电力系统 BuildableData 资产（2终端+6电源）。
    /// 放在工业分类下，建造菜单自动按技能等级分前期/中期/后期。
    /// </summary>
    public class CreatePowerBuildables : EditorWindow
    {
        static SkillRequirement[] BuildReq(int level) => level <= 0 ? null :
            new SkillRequirement[] { new SkillRequirement { skill = SkillType.建造拆解, level = level } };
        static SkillRequirement[] Skills(params (SkillType skill, int level)[] list)
        {
            var r = new SkillRequirement[list.Length];
            for (int i = 0; i < list.Length; i++)
                r[i] = new SkillRequirement { skill = list[i].skill, level = list[i].level };
            return r;
        }
        const string FOLDER = "Assets/_Game/Config/BuildableData";
        const string ITEM_FOLDER = "Assets/_Game/Config/Items";
        const string MAT_FOLDER = "Assets/_Game/Config/Items/SemiFinished";

        [MenuItem("Tools/创建电力 BuildableData")]
        public static void CreateAll()
        {
            if (!AssetDatabase.IsValidFolder(FOLDER))
                AssetDatabase.CreateFolder("Assets/_Game/Config", "BuildableData");

            // 先创建电缆物品
            CreateCableItem();

            int created = 0;
            created += CreateTerminal("用电终端", 15f, 2,
                ("铁锭", 4), ("铜锭", 3), ("高级零件", 2));
            created += CreateTerminal("高级终端", 25f, 7,
                ("钢锭", 4), ("铜锭", 4), ("电路板", 1), ("高级零件", 4));
            created += CreatePowerSource("脚踏发电机", PowerSourceType.Human, 20f, BuildReq(1), 5f,
                0f, null, null, false, false, false,
                ("铁锭", 4), ("铜锭", 2), ("高级零件", 4), ("橡胶", 2));
            created += CreatePowerSource("太阳能板", PowerSourceType.Solar, 50f, Skills((SkillType.建造拆解, 5), (SkillType.智力, 3)), 0f,
                0f, null, null, true, true, false,
                ("玻璃板", 8), ("铜锭", 6), ("电路板", 4), ("钢锭", 4));
            created += CreatePowerSource("风车", PowerSourceType.Wind, 60f, BuildReq(5), 10f,
                0f, null, null, false, true, false,
                ("原木", 15), ("布匹", 4), ("铁锭", 4), ("高级零件", 6));
            created += CreatePowerSource("水车", PowerSourceType.Water, 80f, BuildReq(5), 10f,
                0f, null, null, false, false, true,
                ("原木", 20), ("铁锭", 6), ("高级零件", 8));
            created += CreatePowerSource("简易发电机", PowerSourceType.Combustion, 100f, BuildReq(3), 15f,
                1f, "煤矿", "煤矿", false, false, false,
                ("铁锭", 6), ("铜锭", 4), ("高级零件", 8), ("原木", 10));
            created += CreatePowerSource("火力发电站", PowerSourceType.Thermal, 300f, BuildReq(7), 40f,
                3f, "精炼煤炭", "精炼煤炭", false, false, false,
                ("钢锭", 8), ("石砖", 20), ("高级零件", 6), ("铜锭", 4), ("铁锭", 4));

            // 自动加入 BuildableCatalog
            AddToCatalog();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[CreatePowerBuildables] 已创建 {created} 个电力 BuildableData 并加入 Catalog");
            EditorUtility.DisplayDialog("完成", $"已创建 {created} 个电力建造物资产，已自动加入 BuildableCatalog。\n路径: {FOLDER}", "好的");
        }

        static int CreateTerminal(string name, float radius, int skill, params (string item, int count)[] materials)
        {
            string path = $"{FOLDER}/{name}.asset";
            if (AssetDatabase.LoadAssetAtPath<BuildableData>(path) != null)
            {
                Debug.Log($"[CreatePowerBuildables] 跳过（已存在）: {name}");
                return 0;
            }

            var data = ScriptableObject.CreateInstance<BuildableData>();
            data.displayName = name;
            data.description = $"供电终端，半径{radius}m。范围内设备自动通电。可通过电缆连接发电端或其他终端。";
            data.category = BuildableCategory.Power;
            data.placementSize = new Vector3(1f, 1f, 1f);
            data.snapSize = 1f;
            data.buildDuration = 3f;
            data.maxHealth = 200f;
            data.skillRequirements = BuildReq(skill);
            data.powerSupplyRadius = radius;
            data.materials = MakeMaterials(materials);

            AssetDatabase.CreateAsset(data, path);
            Debug.Log($"[CreatePowerBuildables] 创建终端: {name}");
            return 1;
        }

        static int CreatePowerSource(string name, PowerSourceType srcType, float power, SkillRequirement[] skillReqs, float noise,
            float fuelPerHour, string fuelName, string fuelItemLookup, bool daytimeOnly, bool openAir,
            bool needsWater, params (string item, int count)[] materials)
        {
            string path = $"{FOLDER}/{name}.asset";
            if (AssetDatabase.LoadAssetAtPath<BuildableData>(path) != null)
            {
                Debug.Log($"[CreatePowerBuildables] 跳过（已存在）: {name}");
                return 0;
            }

            var data = ScriptableObject.CreateInstance<BuildableData>();
            data.displayName = name;
            data.description = $"发电设备，输出功率 {power}W。放置后需用电缆连接至用电终端。";
            data.category = BuildableCategory.Power;
            data.placementSize = power >= 300f
                ? new Vector3(3f, 2f, 3f)
                : new Vector3(1.5f, 2f, 1.5f);
            data.snapSize = 1f;
            data.buildDuration = power >= 100f ? 8f : 5f;
            data.maxHealth = power >= 200f ? 500f : 200f;
            data.skillRequirements = skillReqs;
            data.powerOutput = power;
            data.powerSourceType = srcType;
            data.powerRequiresFuel = fuelPerHour > 0f;
            data.powerFuelPerHour = fuelPerHour;
            data.powerFuelItemName = fuelName;
            if (!string.IsNullOrEmpty(fuelItemLookup))
                data.powerFuelItemData = FindItem(fuelItemLookup);
            data.powerDaytimeOnly = daytimeOnly;
            data.powerRequiresOpenAir = openAir;
            data.powerRequiresWater = needsWater;
            data.powerNoiseRadius = noise;
            data.materials = MakeMaterials(materials);

            AssetDatabase.CreateAsset(data, path);
            Debug.Log($"[CreatePowerBuildables] 创建电源: {name} ({power}W)");
            return 1;
        }

        static void CreateCableItem()
        {
            string cablePath = $"{MAT_FOLDER}/Cable.asset";
            if (AssetDatabase.LoadAssetAtPath<ItemData>(cablePath) != null)
                return;

            var cable = ScriptableObject.CreateInstance<ItemData>();
            cable.itemName = "电缆";
            cable.description = "铜芯橡胶电缆，用于连接发电端和用电终端。每10m消耗1卷。";
            cable.category = ItemCategory.SemiFinished;
            cable.gridWidth = 1;
            cable.gridHeight = 1;
            cable.maxStack = 20;
            cable.weight = 0.5f;

            AssetDatabase.CreateAsset(cable, cablePath);
            Debug.Log("[CreatePowerBuildables] 创建电缆物品");
        }

        static void AddToCatalog()
        {
            const string catalogPath = "Assets/_Game/Config/BuildableCatalog.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<BuildableCatalog>(catalogPath);
            if (catalog == null)
            {
                Debug.LogWarning("[CreatePowerBuildables] 未找到 BuildableCatalog");
                return;
            }

            // 收集现有的 GUID
            var existingGuids = new System.Collections.Generic.HashSet<string>();
            if (catalog.buildables != null)
                foreach (var b in catalog.buildables)
                    if (b != null)
                        existingGuids.Add(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(b)));

            // 扫描 BuildableData 文件夹，添加不在 catalog 中的
            var guids = AssetDatabase.FindAssets("t:BuildableData", new[] { FOLDER });
            var newList = catalog.buildables != null
                ? new System.Collections.Generic.List<BuildableData>(catalog.buildables)
                : new System.Collections.Generic.List<BuildableData>();

            int added = 0;
            foreach (var g in guids)
            {
                if (existingGuids.Contains(g)) continue;
                var data = AssetDatabase.LoadAssetAtPath<BuildableData>(AssetDatabase.GUIDToAssetPath(g));
                if (data != null)
                {
                    newList.Add(data);
                    existingGuids.Add(g);
                    added++;
                }
            }

            if (added > 0)
            {
                catalog.buildables = newList.ToArray();
                EditorUtility.SetDirty(catalog);
                Debug.Log($"[CreatePowerBuildables] 已向 Catalog 添加 {added} 个建造物");
            }
        }

        static ItemRequirement[] MakeMaterials((string item, int count)[] list)
        {
            var reqs = new ItemRequirement[list.Length];
            for (int i = 0; i < list.Length; i++)
            {
                reqs[i] = new ItemRequirement
                {
                    itemData = FindItem(list[i].item),
                    count = list[i].count
                };
            }
            return reqs;
        }

        static ItemData FindItem(string name)
        {
            // 按 itemName 字段匹配（中文名），而非文件名
            var guids = AssetDatabase.FindAssets("t:ItemData");
            foreach (var g in guids)
            {
                var data = AssetDatabase.LoadAssetAtPath<ItemData>(AssetDatabase.GUIDToAssetPath(g));
                if (data != null && data.itemName == name)
                    return data;
            }
            Debug.LogWarning($"[CreatePowerBuildables] 未找到物品: {name}");
            return null;
        }
    }
}
