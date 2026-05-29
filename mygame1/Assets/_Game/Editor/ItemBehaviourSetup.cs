using System.Collections.Generic;
using _Game.Config;
using UnityEditor;
using UnityEngine;

namespace _Game.Editor
{
    /// <summary>
    /// 批量为 ItemData 添加 ItemBehaviourEntry
    /// 自动从现有字段推断已有系统实现，新功能标记为未实现
    /// </summary>
    public class ItemBehaviourSetup : EditorWindow
    {
        [MenuItem("Tools/物品图谱/配置 ItemData 行为条目")]
        public static void Setup()
        {
            var guids = AssetDatabase.FindAssets("t:ItemData");
            int updated = 0, total = guids.Length;

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
                if (item == null || string.IsNullOrEmpty(item.itemName)) continue;

                // 收集已有系统行为
                var behaviours = new List<ItemBehaviourEntry>(item.behaviours ?? new List<ItemBehaviourEntry>());
                var existingNames = new HashSet<string>();
                foreach (var b in behaviours)
                    if (b != null) existingNames.Add(b.behaviourName);

                // 武器类 → Combat
                if ((item.category == ItemCategory.Equipment || item.category == ItemCategory.Ammo) &&
                    (item.weaponDamage > 0 || item.isFirearm))
                {
                    AddIfNew(behaviours, existingNames, "武器战斗", GameSystem.Combat,
                        "WeaponShooting/PlayerCombat", true, "已有武器系统完整支持");
                }

                // 弹药类 → Combat
                if (item.category == ItemCategory.Ammo)
                {
                    AddIfNew(behaviours, existingNames, "弹药", GameSystem.Combat,
                        "", true, "纯数据驱动， WeaponShooting 读取 ItemData.weaponDamage 等字段");
                }

                // 装备护甲 → Equipment
                if (item.armorValue > 0)
                {
                    AddIfNew(behaviours, existingNames, "护甲减伤", GameSystem.Equipment,
                        "", true, "纯数据驱动，装备系统读取 armorValue");
                }

                // 装备容器 → Equipment
                if (item.storageWidth > 0 || item.storageHeight > 0)
                {
                    AddIfNew(behaviours, existingNames, "容器扩容", GameSystem.Equipment,
                        "", true, "纯数据驱动，装备系统读取 storageWidth/Height");
                }

                // 生存效果 → Survival
                if (item.itemEffects != null && item.itemEffects.Count > 0)
                {
                    AddIfNew(behaviours, existingNames, "使用效果", GameSystem.Survival,
                        "", true, "纯数据驱动， SurvivalSystem 读取 itemEffects");
                }

                // 保暖 → Survival
                if (item.warmthValue > 0)
                {
                    AddIfNew(behaviours, existingNames, "保暖效果", GameSystem.Survival,
                        "", true, "纯数据驱动，温度系统读取 warmthValue");
                }

                // 工作台 → Building + Crafting
                if (item.isWorkstation)
                {
                    AddIfNew(behaviours, existingNames, "工作台交互", GameSystem.Building,
                        "", true, "建造系统 + WorkstationInteract 处理");
                    AddIfNew(behaviours, existingNames, "合成面板", GameSystem.Crafting,
                        "", true, "CraftingUI 打开对应工作台等级配方");
                }

                // 工作台等级 → Crafting
                if (item.workstationTier != WorkstationTier.Hands && !item.isWorkstation)
                {
                    AddIfNew(behaviours, existingNames, "配方产出", GameSystem.Crafting,
                        "", true, "纯数据驱动，RecipeData 引用此 ItemData 作为产出");
                }

                // 特殊物品 → 按名称匹配
                ApplyNamedBehaviours(item, behaviours, existingNames);

                // 有变更才写入
                if (behaviours.Count != (item.behaviours?.Count ?? 0) || HasChanges(item.behaviours, behaviours))
                {
                    Undo.RecordObject(item, "添加行为条目");
                    item.behaviours = behaviours;
                    EditorUtility.SetDirty(item);
                    updated++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ItemBehaviourSetup] 完成！扫描 {total} 个 ItemData，更新 {updated} 个");
            EditorUtility.DisplayDialog("行为条目配置完成",
                $"扫描 {total} 个物品\n更新 {updated} 个\n\n所有 .asset 已保存，重新构建 ItemGraph 即可看到系统追踪数据。",
                "确定");
        }

        static void AddIfNew(List<ItemBehaviourEntry> list, HashSet<string> existing,
            string name, GameSystem sys, string component, bool implemented, string notes)
        {
            if (existing.Contains(name)) return;
            existing.Add(name);
            list.Add(new ItemBehaviourEntry
            {
                behaviourName = name,
                system = sys,
                componentTypeName = component,
                implemented = implemented,
                notes = notes
            });
        }

        static bool HasChanges(List<ItemBehaviourEntry> oldList, List<ItemBehaviourEntry> newList)
        {
            if (oldList == null) return newList.Count > 0;
            return oldList.Count != newList.Count;
        }

        /// <summary>
        /// 按物品名称匹配特殊行为（新设计但脚本未实现的功能）
        /// </summary>
        static void ApplyNamedBehaviours(ItemData item, List<ItemBehaviourEntry> list, HashSet<string> existing)
        {
            var name = item.itemName;

            // === 已有功能，标记为已实现 ===

            // AI 机器人相关
            if (name.Contains("AI"))
            {
                AddIfNew(list, existing, "AI机器人", GameSystem.AIBot,
                    "AIBot", true, "AIBot 系统完整");
            }

            // 车辆相关
            if (name.Contains("车") || name.Contains("引擎") || name.Contains("车门") || name.Contains("轮胎"))
            {
                AddIfNew(list, existing, "载具", GameSystem.Vehicle,
                    "VehicleController", true, "已有载具系统");
            }

            // 电力设备（太阳能板、发电机、核电站等）
            if (name.Contains("太阳能") || name.Contains("发电机") || name.Contains("风车") ||
                name.Contains("水车") || name.Contains("火力发电") || name.Contains("核电站") ||
                name.Contains("用电终端") || name.Contains("高级终端"))
            {
                AddIfNew(list, existing, "电力", GameSystem.Power,
                    "PowerSource/PowerTerminal", true, "已有电力系统完整支持");
            }

            // 电力相关物品
            if (name.Contains("电池") || name.Contains("电缆") || name.Contains("线圈"))
            {
                AddIfNew(list, existing, "电力组件", GameSystem.Power,
                    "", true, "纯数据驱动");
            }

            // 天气/环境
            if (name.Contains("气象") || name.Contains("卫星"))
            {
                AddIfNew(list, existing, "环境探测", GameSystem.Environment,
                    "Meteorologist/SatelliteDish", false, "气象预测仪/卫星接收站脚本未实现");
            }

            // === 新设计物品，标记为未实现 ===

            // 光学/夜视
            if (name.Contains("夜视"))
            {
                AddIfNew(list, existing, "夜视效果", GameSystem.Equipment,
                    "NightVisionEffect", false, "装备后屏幕变绿增亮，夜间有效");
            }

            // 传感器/探测
            if (name.Contains("传感器"))
            {
                AddIfNew(list, existing, "运动探测", GameSystem.Detection,
                    "MotionDetector", false, "放置后 20m 内检测僵尸移动，闪灯发声");
            }

            // 环境监测
            if (name.Contains("环境监测"))
            {
                AddIfNew(list, existing, "环境监测", GameSystem.Environment,
                    "EnvironmentMonitor", false, "显示辐射/毒气浓度数据");
            }

            // 自动门
            if (name.Contains("自动门"))
            {
                AddIfNew(list, existing, "自动开关", GameSystem.Building,
                    "AutoDoor", false, "靠近自动开，离开自动关");
            }

            // 捕兽夹/陷阱
            if (name.Contains("捕兽夹") || name.Contains("陷阱"))
            {
                AddIfNew(list, existing, "陷阱捕捉", GameSystem.Hunting,
                    "Trap", false, "放置后自动捕捉小型动物");
            }

            // 铝合金相关
            if (name.Contains("铝合金"))
            {
                if (name.Contains("车门") || name.Contains("引擎罩"))
                    AddIfNew(list, existing, "载具配件", GameSystem.Vehicle,
                        "VehiclePart", false, "载具改装系统未实现，需挂载到车辆改属性");
                else if (name.Contains("胸甲"))
                    AddIfNew(list, existing, "轻量护甲", GameSystem.Equipment,
                        "", true, "纯数据驱动，读取 armorValue");
                else if (name.Contains("盾"))
                    AddIfNew(list, existing, "轻量盾牌", GameSystem.Equipment,
                        "", true, "纯数据驱动");
                else if (name.Contains("窗框"))
                    AddIfNew(list, existing, "建筑", GameSystem.Building,
                        "", true, "纯建造物，BuildableData 配置");
                else if (name.Contains("水车"))
                    AddIfNew(list, existing, "电力增强", GameSystem.Power,
                        "", true, "纯数据驱动，提升水车发电效率");
            }

            // 手摇发电机
            if (name.Contains("手摇发电机"))
            {
                AddIfNew(list, existing, "人力发电", GameSystem.Power,
                    "PowerSource", true, "复用已有 PowerSource， Human 类型");
            }

            // 碳纤装备
            if (name.Contains("碳纤"))
            {
                if (name.Contains("甲") || name.Contains("盾"))
                    AddIfNew(list, existing, "碳纤护甲", GameSystem.Equipment,
                        "", true, "纯数据驱动，读取 armorValue");
                else if (name.Contains("弓"))
                    AddIfNew(list, existing, "碳纤弓", GameSystem.Combat,
                        "WeaponShooting", false, "远程武器，复用武器系统但要新建 ItemData");
            }

            // 消音器
            if (name.Contains("消音器"))
            {
                AddIfNew(list, existing, "消音效果", GameSystem.Combat,
                    "SuppressorEffect", false, "装备后降低枪声分贝，武器附件系统未实现");
            }

            // 半自动/全自动改造
            if (name.Contains("半自动改造") || name.Contains("全自动改造"))
            {
                AddIfNew(list, existing, "武器改造", GameSystem.Combat,
                    "WeaponModKit", false, "武器改装系统未实现，替换 fireRate 等参数");
            }

            // 密码破译器
            if (name.Contains("密码破译"))
            {
                AddIfNew(list, existing, "密码破译", GameSystem.Other,
                    "Decryptor", false, "开锁军用设施/保险库，需要任务系统支持");
            }

            // 自动化编程台
            if (name.Contains("自动化编程"))
            {
                AddIfNew(list, existing, "设备自动化", GameSystem.Building,
                    "AutomationController", false, "设备自动取料加燃料，全自动工厂");
            }

            // AI核心
            if (name.Contains("AI核心"))
            {
                AddIfNew(list, existing, "AI核心", GameSystem.AIBot,
                    "AICore", false, "跟随机器人，自动搜刮+电击防身，智Lv10");
            }

            // 疫苗相关
            if (name.Contains("疫苗") || name.Contains("免疫") || name.Contains("器官修复") || name.Contains("神经再生"))
            {
                AddIfNew(list, existing, "高级医疗", GameSystem.Survival,
                    "", false, "效果脚本未实现（ItemEffect 枚举需扩展：咬伤免疫/永久伤恢复/技能恢复）");
            }
        }
    }
}
