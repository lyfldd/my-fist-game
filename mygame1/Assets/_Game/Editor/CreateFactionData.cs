using UnityEditor;
using UnityEngine;
using _Game.Config;

namespace _Game.Editor
{
    /// <summary>
    /// 一键生成7个阵营 FactionData SO
    /// </summary>
    public static class CreateFactionData
    {
        const string OUTPUT_PATH = "Assets/_Game/Config/Factions/";

        [MenuItem("Tools/创建默认阵营数据")]
        public static void CreateAll()
        {
            if (!AssetDatabase.IsValidFolder(OUTPUT_PATH))
            {
                string parent = OUTPUT_PATH.TrimEnd('/');
                string folder = "Factions";
                AssetDatabase.CreateFolder("Assets/_Game/Config", folder);
            }

            var specs = new (FactionType type, string name, Color color, FactionType[] allies, FactionType[] hostiles)[]
            {
                (FactionType.Player,     "玩家",   new Color(0.2f, 0.6f, 1f),
                    new[]{ FactionType.Survivor, FactionType.AIBot },
                    new[]{ FactionType.Zombie, FactionType.Bandit }),
                (FactionType.Survivor,   "幸存者", new Color(0.3f, 0.8f, 0.3f),
                    new[]{ FactionType.Player, FactionType.AIBot, FactionType.Military },
                    new[]{ FactionType.Zombie, FactionType.Bandit }),
                (FactionType.Zombie,     "僵尸",   new Color(0.8f, 0.2f, 0.2f),
                    new FactionType[]{},
                    new[]{ FactionType.Player, FactionType.Survivor, FactionType.AIBot, FactionType.Bandit, FactionType.Military, FactionType.Neutral }),
                (FactionType.AIBot,      "AI机器人", new Color(0.6f, 0.6f, 0.6f),
                    new[]{ FactionType.Player, FactionType.Survivor },
                    new[]{ FactionType.Zombie, FactionType.Bandit }),
                (FactionType.Bandit,     "黑恶势力", new Color(0.8f, 0.4f, 0.1f),
                    new FactionType[]{},
                    new[]{ FactionType.Player, FactionType.Survivor, FactionType.AIBot, FactionType.Military, FactionType.Zombie }),
                (FactionType.Military,   "军方",   new Color(0.2f, 0.5f, 0.2f),
                    new FactionType[]{},
                    new[]{ FactionType.Zombie, FactionType.Bandit }),
                (FactionType.Neutral,    "中立",   new Color(0.7f, 0.7f, 0.5f),
                    new FactionType[]{},
                    new FactionType[]{}),
            };

            foreach (var spec in specs)
            {
                var asset = ScriptableObject.CreateInstance<FactionData>();
                asset.factionType = spec.type;
                asset.displayName = spec.name;
                asset.factionColor = spec.color;
                asset.allies = spec.allies;
                asset.hostiles = spec.hostiles;

                string filename = $"Faction_{spec.type}.asset";
                AssetDatabase.CreateAsset(asset, OUTPUT_PATH + filename);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[CreateFactionData] 7 个阵营 SO 创建完成 → " + OUTPUT_PATH);
        }
    }
}
