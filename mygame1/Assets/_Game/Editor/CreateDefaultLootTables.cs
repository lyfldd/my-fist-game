using UnityEngine;
using UnityEditor;
using _Game.Config;
using _Game.Core;

namespace _Game.Editor
{
    /// <summary>
    /// 一键创建 4 个容器 Loot 配置（Profile + LootTable）
    /// 菜单：Game → 创建默认容器配置
    /// </summary>
    public static class CreateDefaultLootTables
    {
        private const string CONFIG_PATH = "Assets/_Game/Config/";
        private const string LOOT_PATH = CONFIG_PATH + "LootTables/";
        private const string PROFILE_PATH = CONFIG_PATH + "ContainerProfiles/";

        [MenuItem("Game/创建默认容器配置 (4 Profiles + 4 LootTables)")]
        public static void CreateDefaults()
        {
            EnsureDir(LOOT_PATH);
            EnsureDir(PROFILE_PATH);

            // === LootTables ===
            var fridgeLoot   = CreateAsset<LootTable>(LOOT_PATH, "FridgeLoot");
            var cabinetLoot  = CreateAsset<LootTable>(LOOT_PATH, "CabinetLoot");
            var corpseLoot   = CreateAsset<LootTable>(LOOT_PATH, "CorpseLoot");
            var crateLoot    = CreateAsset<LootTable>(LOOT_PATH, "CrateLoot");

            // === ContainerLootProfiles ===
            // 冰箱：食品为主，4×3 格子，搜 1.5s
            var fridgeProfile = CreateAsset<ContainerLootProfile>(PROFILE_PATH, "FridgeProfile");
            SetupProfile(fridgeProfile, "冰箱", fridgeLoot, 4, 3, 1.5f, 0.1f, 1, 3,
                GameConstants.CONTAINER_TAG_FRIDGE);

            // 柜子：杂物为主，6×4 格子，搜 2s
            var cabinetProfile = CreateAsset<ContainerLootProfile>(PROFILE_PATH, "CabinetProfile");
            SetupProfile(cabinetProfile, "柜子", cabinetLoot, 6, 4, 2f, 0.1f, 1, 3,
                GameConstants.CONTAINER_TAG_CABINET);

            // 尸体：少量杂物+武器，3×2 格子，搜 1s
            var corpseProfile = CreateAsset<ContainerLootProfile>(PROFILE_PATH, "CorpseProfile");
            SetupProfile(corpseProfile, "尸体", corpseLoot, 3, 2, 1f, 0.1f, 1, 2,
                GameConstants.CONTAINER_TAG_CORPSE);

            // 板条箱：工具材料为主，4×5 格子，搜 2.5s
            var crateProfile = CreateAsset<ContainerLootProfile>(PROFILE_PATH, "CrateProfile");
            SetupProfile(crateProfile, "板条箱", crateLoot, 4, 5, 2.5f, 0.1f, 1, 3,
                GameConstants.CONTAINER_TAG_CRATE);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 选中第一个 Profile
            Selection.activeObject = fridgeProfile;

            Debug.Log("[CreateDefaultLootTables] 完成！已创建 4 Profiles + 4 LootTables\n" +
                      $"  Profiles: {PROFILE_PATH}\n  LootTables: {LOOT_PATH}\n" +
                      "  请在各 LootTable 中配置具体物品条目（entries）。");
        }

        // ===== 辅助方法 =====

        private static void EnsureDir(string path)
        {
            // 去除末尾斜杠
            string dir = path.TrimEnd('/');
            string parent = System.IO.Path.GetDirectoryName(dir).Replace('\\', '/');
            string name = System.IO.Path.GetFileName(dir);

            if (!AssetDatabase.IsValidFolder(dir))
                AssetDatabase.CreateFolder(parent, name);
        }

        private static T CreateAsset<T>(string folder, string assetName) where T : ScriptableObject
        {
            string path = $"{folder}{assetName}.asset";

            // 已存在则跳过创建
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                Debug.Log($"[跳过] {path} 已存在");
                return existing;
            }

            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void SetupProfile(
            ContainerLootProfile profile,
            string displayName,
            LootTable lootTable,
            int gridWidth, int gridHeight,
            float searchTime, float emptyChance,
            int minTypes, int maxTypes,
            string containerTag)
        {
            profile.displayName   = displayName;
            profile.lootTable     = lootTable;
            profile.gridWidth     = gridWidth;
            profile.gridHeight    = gridHeight;
            profile.searchTime    = searchTime;
            profile.emptyChance   = emptyChance;
            profile.minLootTypes  = minTypes;
            profile.maxLootTypes  = maxTypes;
            profile.containerTag  = containerTag;

            EditorUtility.SetDirty(profile);
        }
    }
}
