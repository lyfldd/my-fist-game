using UnityEngine;
using UnityEditor;
using _Game.Config;
using _Game.Systems.Power;

namespace _Game.Editor
{
    /// <summary>
    /// 一键配置电力组件参数。
    /// 先运行 Tools→创建电力 BuildableData，再运行本工具。
    /// 终端/电源无需本工具（CreatePowerBuildables 已写入 BuildableData），
    /// 本工具主要为生产设备 BuildableData 写入 powerRequired 等用电字段。
    /// </summary>
    public class SetupPowerComponents : EditorWindow
    {
        [MenuItem("Tools/配置电力组件参数")]
        public static void SetupAll()
        {
            int configured = 0;
            configured += SetupTerminals();
            configured += SetupPowerSources();
            configured += SetupPowerConsumers();
            configured += MigrateOldGenerator();
            configured += CleanupNonProductionDevices();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[SetupPowerComponents] 已配置/清理 {configured} 个设备");
            EditorUtility.DisplayDialog("完成", $"已配置/清理 {configured} 个设备。\n\n未找到的会在 Console 提示。", "好的");
        }

        // ================================================================
        // 终端（prefab 组件配置，无 prefab 则跳过）
        // ================================================================

        static int SetupTerminals()
        {
            int count = 0;
            count += Config("用电终端", (PowerTerminal pt) =>
            {
                pt.supplyRadius = 15f;
                pt.allowCoalBackup = true;
            });
            count += Config("高级终端", (PowerTerminal pt) =>
            {
                pt.supplyRadius = 25f;
                pt.allowCoalBackup = true;
            });
            return count;
        }

        // ================================================================
        // 电源（prefab 组件配置，无 prefab 则跳过）
        // ================================================================

        static int SetupPowerSources()
        {
            int count = 0;

            count += ConfigSrc("脚踏发电机", ps =>
            {
                ps.sourceType = PowerSourceType.Human;
                ps.maxOutput = 20f;
                ps.requiresFuel = false;
                ps.noiseRadius = 5f;
            });

            count += ConfigSrc("太阳能板", ps =>
            {
                ps.sourceType = PowerSourceType.Solar;
                ps.maxOutput = 50f;
                ps.requiresFuel = false;
                ps.daytimeOnly = true;
                ps.requiresOpenAir = true;
                ps.noiseRadius = 0f;
            });

            count += ConfigSrc("风车", ps =>
            {
                ps.sourceType = PowerSourceType.Wind;
                ps.maxOutput = 60f;
                ps.requiresFuel = false;
                ps.requiresOpenAir = true;
                ps.noiseRadius = 10f;
            });

            count += ConfigSrc("水车", ps =>
            {
                ps.sourceType = PowerSourceType.Water;
                ps.maxOutput = 80f;
                ps.requiresFuel = false;
                ps.noiseRadius = 10f;
            });

            count += ConfigSrc("简易发电机", ps =>
            {
                ps.sourceType = PowerSourceType.Combustion;
                ps.maxOutput = 100f;
                ps.requiresFuel = true;
                ps.fuelPerHour = 1f;
                ps.fuelItemName = "汽油";
                ps.noiseRadius = 15f;
            });

            count += ConfigSrc("火力发电站", ps =>
            {
                ps.sourceType = PowerSourceType.Thermal;
                ps.maxOutput = 300f;
                ps.requiresFuel = true;
                ps.fuelPerHour = 3f;
                ps.fuelItemName = "精炼煤炭";
                ps.noiseRadius = 40f;
            });

            return count;
        }

        // ================================================================
        // 设备用电端 — 匹配项目中实际存在的 BuildableData.displayName
        // ================================================================

        static int SetupPowerConsumers()
        {
            int count = 0;

            // 前期 — 可烧煤兜底
            count += ConfigConsumer("熔炉", 20, true, 10, 1.5f);
            count += ConfigConsumer("水泵", 15, true, 8, 1.5f);
            count += ConfigConsumer("炭窑", 20, true, 10, 2);

            // 中期 — 可烧煤兜底
            count += ConfigConsumer("蒸馏器", 30, true, 15, 2);
            count += ConfigConsumer("制药台", 30, true, 15, 2);
            count += ConfigConsumer("织布机", 25, false, 0, 3);
            count += ConfigConsumer("装配台", 35, true, 15, 1.5f);
            count += ConfigConsumer("发酵罐", 40, true, 15, 2);
            count += ConfigConsumer("锯木机", 25, true, 10, 3);
            count += ConfigConsumer("熏制房", 25, true, 10, 1.5f);

            // 后期 — 可烧煤兜底
            count += ConfigConsumer("工业炉", 50, true, 20, 3);
            count += ConfigConsumer("化学台", 55, true, 20, 1.5f);
            count += ConfigConsumer("电解槽", 60, true, 20, 2);
            count += ConfigConsumer("机械加工台", 35, true, 15, 2);
            count += ConfigConsumer("回收站", 45, true, 15, 2);

            // 后期 — 必须电力（无烧煤兜底）
            count += ConfigConsumer("冲压机", 40, false, 0, 3);
            count += ConfigConsumer("车床", 30, false, 0, 3);
            count += ConfigConsumer("粉碎机", 15, false, 0, 2);
            count += ConfigConsumer("罐头封装机", 35, false, 0, 2);

            return count;
        }

        // ================================================================
        // 迁移旧"发电机" → 电力系统
        // ================================================================

        static int MigrateOldGenerator()
        {
            var data = FindBuildable("发电机");
            if (data == null) return 0;

            // 已迁移过则跳过
            if (data.category == BuildableCategory.Power && data.powerOutput > 0f)
            {
                Debug.Log("[SetupPower] 旧发电机已迁移，跳过");
                return 0;
            }

            data.category = BuildableCategory.Power;
            data.powerOutput = 200f;
            data.powerSourceType = PowerSourceType.Combustion;
            data.powerRequiresFuel = true;
            data.powerFuelPerHour = 2f;
            data.powerFuelItemName = "煤矿";
            data.powerFuelItemData = FindItem("煤矿");
            data.powerNoiseRadius = 25f;
            data.description = "中型燃油发电机，输出200W。需用电缆连接至用电终端。";
            // 清除旧的 ProductionDevice 引用，改为纯电源
            data.productionDeviceRef = null;

            EditorUtility.SetDirty(data);
            Debug.Log("[SetupPower] 已将旧「发电机」迁移为电力系统电源 (200W, Combustion)");
            return 1;
        }

        // ================================================================
        // 清理不合理生产设备（广播塔/太阳能板→纯建筑/电源）
        // ================================================================

        static int CleanupNonProductionDevices()
        {
            int cleaned = 0;

            // 广播塔：清除 ProductionDevice，保留为功能性建筑
            cleaned += ClearProductionRef("广播塔");
            cleaned += DeleteProductionDeviceAsset("RadioTower");

            // 太阳能板：已迁移为电源，清除旧 ProductionDevice
            cleaned += ClearProductionRef("太阳能板");
            cleaned += DeleteProductionDeviceAsset("SolarPanel");

            return cleaned;
        }

        static int ClearProductionRef(string displayName)
        {
            var data = FindBuildable(displayName);
            if (data == null || data.productionDeviceRef == null) return 0;
            data.productionDeviceRef = null;
            EditorUtility.SetDirty(data);
            Debug.Log($"[SetupPower] 已清除 {displayName} 的 ProductionDeviceRef");
            return 1;
        }

        static int DeleteProductionDeviceAsset(string assetName)
        {
            string path = $"Assets/_Game/Config/ProductionDevices/{assetName}.asset";
            if (!AssetDatabase.LoadAssetAtPath<ProductionDeviceData>(path)) return 0;
            AssetDatabase.DeleteAsset(path);
            Debug.Log($"[SetupPower] 已删除 ProductionDeviceData: {assetName}");
            return 1;
        }

        // ================================================================
        // 辅助方法
        // ================================================================

        static int Config<T>(string name, System.Action<T> action) where T : Component
        {
            var data = FindBuildable(name);
            if (data == null) return 0;

            if (data.builtPrefab == null)
            {
                Debug.LogWarning($"[SetupPower] {name}: builtPrefab 未设置，跳过 prefab 组件配置（BuildableData 已有参数）");
                return 0;
            }

            var comp = data.builtPrefab.GetComponent<T>();
            if (comp == null) comp = data.builtPrefab.AddComponent<T>();
            action(comp);
            EditorUtility.SetDirty(data.builtPrefab);
            Debug.Log($"[SetupPower] 已配置: {name}");
            return 1;
        }

        static int ConfigSrc(string name, System.Action<PowerSource> action) => Config<PowerSource>(name, action);

        static int ConfigConsumer(string name, float power, bool allowCoal, float coalPower, float speedMul)
        {
            var data = FindBuildable(name);
            if (data == null) return 0;

            // 直接写入 BuildableData（BuildModeController 运行时从此读取）
            data.powerRequired = power;
            data.powerAllowCoal = allowCoal;
            data.powerCoalPower = coalPower;
            data.powerElectricSpeedMul = speedMul;
            EditorUtility.SetDirty(data);

            // 如果 prefab 存在，同时配置 PowerConsumer 组件
            if (data.builtPrefab != null)
            {
                var pc = data.builtPrefab.GetComponent<PowerConsumer>();
                if (pc == null) pc = data.builtPrefab.AddComponent<PowerConsumer>();
                pc.requiredPower = power;
                pc.allowCoal = allowCoal;
                pc.coalPower = coalPower;
                pc.electricSpeedMultiplier = speedMul;
                pc.zeroWasteOnElectric = true;
                EditorUtility.SetDirty(data.builtPrefab);
            }

            Debug.Log($"[SetupPower] 已配置用电设备: {name} ({power}W, coal={allowCoal})");
            return 1;
        }

        static BuildableData FindBuildable(string name)
        {
            var guids = AssetDatabase.FindAssets("t:BuildableData");
            foreach (var g in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                var data = AssetDatabase.LoadAssetAtPath<BuildableData>(path);
                if (data != null && data.displayName == name) return data;
            }
            Debug.LogWarning($"[SetupPower] 未找到 BuildableData: {name}");
            return null;
        }

        static ItemData FindItem(string name)
        {
            var guids = AssetDatabase.FindAssets("t:ItemData");
            foreach (var g in guids)
            {
                var data = AssetDatabase.LoadAssetAtPath<ItemData>(AssetDatabase.GUIDToAssetPath(g));
                if (data != null && data.itemName == name) return data;
            }
            Debug.LogWarning($"[SetupPower] 未找到物品: {name}");
            return null;
        }
    }
}
