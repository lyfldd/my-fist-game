using System;
using System.Collections.Generic;
using System.Linq;
using _Game.Config;
using UnityEditor;
using UnityEngine;

namespace _Game.Editor
{
    /// <summary>
    /// 物品图谱构建器 — 扫描全部 RecipeData，构建上下游依赖图谱
    /// 自动计算深度、检测断头路、按规则分配工作台等级
    /// </summary>
    public class ItemGraphBuilder : EditorWindow
    {
        [MenuItem("Tools/物品图谱/构建 ItemGraph")]
        public static void Build()
        {
            // 1. 收集所有 ItemData
            var allItemNames = new HashSet<string>();
            var itemDataGuids = AssetDatabase.FindAssets("t:ItemData");
            foreach (var guid in itemDataGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var itemData = AssetDatabase.LoadAssetAtPath<ItemData>(path);
                if (itemData != null && !string.IsNullOrEmpty(itemData.itemName))
                    allItemNames.Add(itemData.itemName);
            }

            // 2. 收集所有 RecipeData
            var allRecipes = new List<RecipeData>();
            var recipeGuids = AssetDatabase.FindAssets("t:RecipeData");
            foreach (var guid in recipeGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var recipe = AssetDatabase.LoadAssetAtPath<RecipeData>(path);
                if (recipe != null && recipe.resultItem != null)
                    allRecipes.Add(recipe);
            }

            Debug.Log($"[ItemGraphBuilder] 找到 {allItemNames.Count} 个 ItemData, {allRecipes.Count} 个 RecipeData");

            // 3. 构建邻接表
            // upstreamMap[itemName] = 哪些物品是它的材料（它由什么做成）
            // downstreamMap[itemName] = 哪些物品消费它（它被谁用）
            var upstreamMap = new Dictionary<string, HashSet<string>>();
            var downstreamMap = new Dictionary<string, HashSet<string>>();
            // recipeCountForOutput[itemName] = 产出该物品的配方数
            var recipeCountForOutput = new Dictionary<string, int>();

            // 初始化所有已知物品
            foreach (var name in allItemNames)
            {
                upstreamMap[name] = new HashSet<string>();
                downstreamMap[name] = new HashSet<string>();
                recipeCountForOutput[name] = 0;
            }

            foreach (var recipe in allRecipes)
            {
                var outputName = recipe.resultItem.itemName;
                if (string.IsNullOrEmpty(outputName)) continue;

                // 确保输出物品在字典中
                if (!upstreamMap.ContainsKey(outputName))
                {
                    upstreamMap[outputName] = new HashSet<string>();
                    downstreamMap[outputName] = new HashSet<string>();
                    recipeCountForOutput[outputName] = 0;
                    allItemNames.Add(outputName);
                }
                recipeCountForOutput[outputName]++;

                // 记录上下游关系
                if (recipe.materials != null)
                {
                    foreach (var mat in recipe.materials)
                    {
                        if (mat.itemData == null || string.IsNullOrEmpty(mat.itemData.itemName)) continue;
                        var inputName = mat.itemData.itemName;

                        // 确保输入物品在字典中
                        if (!upstreamMap.ContainsKey(inputName))
                        {
                            upstreamMap[inputName] = new HashSet<string>();
                            downstreamMap[inputName] = new HashSet<string>();
                            recipeCountForOutput[inputName] = 0;
                            allItemNames.Add(inputName);
                        }

                        // output 的上游包含 input
                        upstreamMap[outputName].Add(inputName);
                        // input 的下游包含 output
                        downstreamMap[inputName].Add(outputName);
                    }
                }
            }

            // 4. 识别原材料：无配方产出它的物品
            var rawMaterials = new HashSet<string>();
            foreach (var name in allItemNames)
            {
                if (recipeCountForOutput[name] == 0)
                    rawMaterials.Add(name);
            }

            // 5. 拓扑排序 + 深度计算
            var depths = new Dictionary<string, HashSet<int>>(); // itemName → {所有路径深度}
            foreach (var name in allItemNames)
                depths[name] = new HashSet<int>();

            // 原材料深度 = {0}
            foreach (var name in rawMaterials)
                depths[name].Add(0);

            // 构建反向索引：item → 产出它的配方列表
            var recipesByOutput = new Dictionary<string, List<RecipeData>>();
            foreach (var recipe in allRecipes)
            {
                var outputName = recipe.resultItem.itemName;
                if (string.IsNullOrEmpty(outputName)) continue;
                if (!recipesByOutput.ContainsKey(outputName))
                    recipesByOutput[outputName] = new List<RecipeData>();
                recipesByOutput[outputName].Add(recipe);
            }

            // BFS 迭代直到稳定
            bool changed = true;
            int maxIterations = 50;
            int iteration = 0;
            while (changed && iteration < maxIterations)
            {
                changed = false;
                iteration++;

                foreach (var kv in recipesByOutput)
                {
                    var itemName = kv.Key;
                    var itemRecipes = kv.Value;

                    foreach (var recipe in itemRecipes)
                    {
                        if (recipe.materials == null || recipe.materials.Length == 0)
                        {
                            // 无材料配方 → depth = 1 (凭空手搓)
                            if (!depths[itemName].Contains(1))
                            {
                                depths[itemName].Add(1);
                                changed = true;
                            }
                            continue;
                        }

                        // 检查所有材料是否已有深度
                        int maxInputDepth = -1;
                        bool allReady = true;
                        foreach (var mat in recipe.materials)
                        {
                            if (mat.itemData == null) continue;
                            var matName = mat.itemData.itemName;
                            if (!depths.ContainsKey(matName) || depths[matName].Count == 0)
                            {
                                allReady = false;
                                break;
                            }
                            int matMinDepth = depths[matName].Min();
                            if (matMinDepth > maxInputDepth)
                                maxInputDepth = matMinDepth;
                        }

                        if (allReady && maxInputDepth >= 0)
                        {
                            int newDepth = maxInputDepth + 1;
                            if (!depths[itemName].Contains(newDepth))
                            {
                                depths[itemName].Add(newDepth);
                                changed = true;
                            }
                        }
                    }
                }
            }

            // 循环打破：检查未解决深度的物品
            var unresolved = allItemNames.Where(n => depths[n].Count == 0).ToList();
            if (unresolved.Count > 0)
            {
                Debug.LogWarning($"[ItemGraphBuilder] {unresolved.Count} 个物品深度未解决（可能存在循环依赖），尝试打破...");

                // 首先：无配方产出的物品 → 强制深度{0}
                foreach (var name in unresolved)
                {
                    if (recipeCountForOutput[name] == 0)
                    {
                        depths[name].Add(0);
                        rawMaterials.Add(name);
                        Debug.Log($"  [循环打破] {name} → 深度[0]（无配方产出，视为原材料）");
                    }
                }
                unresolved = allItemNames.Where(n => depths[n].Count == 0).ToList();

                // 其次：有配方但材料全无深度 → 取最简单路线（最少材料）→ 深度{1}
                if (unresolved.Count > 0)
                {
                    var byOutput = recipesByOutput;
                    foreach (var name in unresolved)
                    {
                        if (!byOutput.TryGetValue(name, out var itemRecipes) || itemRecipes.Count == 0)
                        {
                            depths[name].Add(0);
                            rawMaterials.Add(name);
                            Debug.Log($"  [循环打破] {name} → 深度[0]（无任何配方产出）");
                            continue;
                        }

                        // 找材料最少的配方
                        var simplest = itemRecipes
                            .OrderBy(r => r.materials?.Length ?? 999)
                            .First();
                        int matCount = simplest.materials?.Length ?? 0;
                        if (matCount == 0)
                        {
                            depths[name].Add(1);
                        }
                        else
                        {
                            // 用已知深度的材料计算，忽略未知的
                            int maxKnownDepth = 0;
                            bool hasAtLeastOne = false;
                            foreach (var mat in simplest.materials)
                            {
                                if (mat.itemData == null) continue;
                                if (depths.TryGetValue(mat.itemData.itemName, out var md) && md.Count > 0)
                                {
                                    int d = md.Min();
                                    if (d > maxKnownDepth) maxKnownDepth = d;
                                    hasAtLeastOne = true;
                                }
                            }
                            depths[name].Add(hasAtLeastOne ? maxKnownDepth + 1 : 1);
                        }
                        Debug.Log($"  [循环打破] {name} → 深度[{string.Join(",", depths[name])}]（循环依赖，取最简路径）");
                    }
                }

                // 重新传播一轮
                changed = true;
                iteration = 0;
                while (changed && iteration < 30)
                {
                    changed = false;
                    iteration++;
                    foreach (var kv in recipesByOutput)
                    {
                        var itemName = kv.Key;
                        foreach (var recipe in kv.Value)
                        {
                            if (recipe.materials == null || recipe.materials.Length == 0)
                            {
                                if (!depths[itemName].Contains(1)) { depths[itemName].Add(1); changed = true; }
                                continue;
                            }
                            int maxInputDepth = -1;
                            bool allReady = true;
                            foreach (var mat in recipe.materials)
                            {
                                if (mat.itemData == null) continue;
                                var matName = mat.itemData.itemName;
                                if (!depths.TryGetValue(matName, out var md) || md.Count == 0)
                                { allReady = false; break; }
                                int matMinDepth = md.Min();
                                if (matMinDepth > maxInputDepth) maxInputDepth = matMinDepth;
                            }
                            if (allReady && maxInputDepth >= 0)
                            {
                                int newDepth = maxInputDepth + 1;
                                if (!depths[itemName].Contains(newDepth))
                                { depths[itemName].Add(newDepth); changed = true; }
                            }
                        }
                    }
                }
            }

            // 最终统计
            var finalUnresolved = allItemNames.Count(n => depths[n].Count == 0);
            Debug.Log($"[ItemGraphBuilder] BFS 完成，迭代 {iteration} 次，原材料 {rawMaterials.Count} 个，循环打破后未解决: {finalUnresolved}");

            // 5.5 构建配方引用查找表
            var producerRecipes = new Dictionary<string, List<RecipeData>>();
            var consumerRecipes = new Dictionary<string, List<RecipeData>>();
            foreach (var name in allItemNames)
            {
                producerRecipes[name] = new List<RecipeData>();
                consumerRecipes[name] = new List<RecipeData>();
            }
            foreach (var recipe in allRecipes)
            {
                var outputName = recipe.resultItem?.itemName;
                if (!string.IsNullOrEmpty(outputName) && producerRecipes.ContainsKey(outputName))
                    producerRecipes[outputName].Add(recipe);

                if (recipe.materials != null)
                {
                    foreach (var mat in recipe.materials)
                    {
                        var matName = mat.itemData?.itemName;
                        if (!string.IsNullOrEmpty(matName) && consumerRecipes.ContainsKey(matName))
                            consumerRecipes[matName].Add(recipe);
                    }
                }
            }

            // 6. 构建 ItemGraphNode 数组
            var itemNameToItemData = new Dictionary<string, ItemData>();
            foreach (var guid in itemDataGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var itemData = AssetDatabase.LoadAssetAtPath<ItemData>(path);
                if (itemData != null && !string.IsNullOrEmpty(itemData.itemName))
                    itemNameToItemData[itemData.itemName] = itemData;
            }

            var nodes = new List<ItemGraphNode>();
            int rawMatCount = 0, deadEndCount = 0, coreCount = 0;

            foreach (var name in allItemNames.OrderBy(n => n))
            {
                var depthArray = depths.ContainsKey(name)
                    ? depths[name].OrderBy(d => d).ToArray()
                    : new int[0];

                var upstreamArr = upstreamMap.ContainsKey(name)
                    ? upstreamMap[name].OrderBy(n => n).ToArray()
                    : new string[0];

                var downstreamArr = downstreamMap.ContainsKey(name)
                    ? downstreamMap[name].OrderBy(n => n).ToArray()
                    : new string[0];

                int consumerCount = downstreamArr.Length;
                bool isRaw = rawMaterials.Contains(name);
                bool isDead = consumerCount == 0;

                if (isRaw) rawMatCount++;
                if (isDead) deadEndCount++;
                if (consumerCount >= 10) coreCount++;

                // 自动检测链类型
                var chainType = DetectChainType(name, itemNameToItemData.ContainsKey(name) ? itemNameToItemData[name] : null);

                // 自动分配工作台（基于 minDepth）
                int minDepth = depthArray.Length > 0 ? depthArray.Min() : 0;
                var autoStation = GetAutoStationStatic(chainType, minDepth);

                // 工业设备等级覆盖：有工业配方产出的物品，取工业设备等级和 autoStation 的最大值
                var industrialMaxTier = WorkstationTier.Hands;
                if (producerRecipes.TryGetValue(name, out var prodRecipes))
                {
                    foreach (var r in prodRecipes)
                    {
                        if (r != null && r.isIndustrial && (int)r.requiredStation > (int)industrialMaxTier)
                            industrialMaxTier = r.requiredStation;
                    }
                }
                var effectiveStation = (int)industrialMaxTier > (int)autoStation ? industrialMaxTier : autoStation;

                // 提取行为依赖
                var requiredSystemsList = new List<string>();
                bool allReady = true;
                if (itemNameToItemData.TryGetValue(name, out var itemData) && itemData.behaviours != null)
                {
                    foreach (var b in itemData.behaviours)
                    {
                        if (b == null) continue;
                        var sysName = b.system.ToString();
                        if (!requiredSystemsList.Contains(sysName))
                            requiredSystemsList.Add(sysName);

                        // 有组件名但未实现 → 标记为未就绪
                        if (!string.IsNullOrEmpty(b.componentTypeName) && !b.implemented)
                            allReady = false;
                    }
                }

                var node = new ItemGraphNode
                {
                    itemName = name,
                    primaryChain = chainType,
                    productionMode = isDead ? ProductionMode.Manual : ProductionMode.Both,
                    overrideStation = false,
                    manualStation = WorkstationTier.Hands,
                    depths = depthArray,
                    upstreamItemNames = upstreamArr,
                    downstreamItemNames = downstreamArr,
                    producerRecipes = producerRecipes.ContainsKey(name) ? producerRecipes[name].ToArray() : new RecipeData[0],
                    consumerRecipes = consumerRecipes.ContainsKey(name) ? consumerRecipes[name].ToArray() : new RecipeData[0],
                    consumerCount = consumerCount,
                    autoAssignedStation = effectiveStation,
                    isRawMaterial = isRaw,
                    isDeadEnd = isDead,
                    requiredSystems = requiredSystemsList.ToArray(),
                    allSystemsReady = allReady
                };

                nodes.Add(node);
            }

            // 7. 保存 ItemGraph.asset
            var graph = CreateInstance<ItemGraph>();
            graph.buildTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            graph.recipeCountAtBuild = allRecipes.Count;
            graph.nodes = nodes.ToArray();
            graph.rawMaterialCount = rawMatCount;
            graph.deadEndCount = deadEndCount;
            graph.coreMaterialCount = coreCount;

            const string savePath = "Assets/_Game/Config/ItemGraph.asset";
            AssetDatabase.CreateAsset(graph, savePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[ItemGraphBuilder] ItemGraph 已保存到 {savePath}");
            Debug.Log($"[ItemGraphBuilder] 统计: 总物品={nodes.Count}, 原材料={rawMatCount}, 断头路={deadEndCount}, 核心材料(热度≥10)={coreCount}");

            // 8. 同步 effectiveStation → RecipeData.requiredStation
            int synced = SyncRecipeStations(nodes);

            // 9. 输出报告
            PrintReport(nodes, rawMatCount, deadEndCount, coreCount);

            EditorUtility.DisplayDialog("ItemGraph 构建完成",
                $"总物品: {nodes.Count}\n原材料: {rawMatCount}\n断头路: {deadEndCount}\n核心材料(热度≥10): {coreCount}\n配方同步: {synced} 个\n\n详情见 Console 窗口",
                "确定");

            Selection.activeObject = graph;

            // 10. 引用完整性检验
            ValidateReferences();
            AssetDatabase.SaveAssets();
        }

        static void ValidateReferences()
        {
            int errors = 0;
            Debug.Log("[ItemGraph] === 引用完整性检验 ===");

            // 1. 配方
            var recipeGuids = AssetDatabase.FindAssets("t:RecipeData");
            int recipeOk = 0;
            foreach (var guid in recipeGuids)
            {
                var r = AssetDatabase.LoadAssetAtPath<RecipeData>(AssetDatabase.GUIDToAssetPath(guid));
                if (r == null) continue;
                bool ok = true;
                if (r.resultItem == null) { Debug.LogError($"❌ 配方产物缺失: {r.name}"); ok = false; errors++; }
                if (r.materials != null)
                    foreach (var m in r.materials)
                        if (m.itemData == null) { Debug.LogError($"❌ 配方材料缺失: {r.name} ← {m.itemData?.itemName ?? "null"}"); ok = false; errors++; }
                if (ok) recipeOk++;
            }
            Debug.Log($"  ✅ 配方: {recipeOk}/{recipeGuids.Length} 有效");

            // 2. 武器弹药
            var itemGuids = AssetDatabase.FindAssets("t:ItemData");
            int weaponOk = 0, weaponErr = 0;
            foreach (var guid in itemGuids)
            {
                var item = AssetDatabase.LoadAssetAtPath<ItemData>(AssetDatabase.GUIDToAssetPath(guid));
                if (item == null || !item.isFirearm) continue;
                if (string.IsNullOrEmpty(item.ammoItemName) && item.ammoItemData == null) { weaponOk++; continue; }
                string ammoName = item.ammoItemData != null ? item.ammoItemData.itemName : item.ammoItemName;
                var found = AssetDatabase.LoadAssetAtPath<ItemData>(AssetDatabase.GUIDToAssetPath(
                    AssetDatabase.FindAssets($"t:ItemData {ammoName}").FirstOrDefault()));
                if (found == null) { Debug.LogError($"❌ 弹药缺失: {item.itemName} → {ammoName}"); weaponErr++; errors++; }
                else weaponOk++;
            }
            Debug.Log($"  ✅ 武器弹药: {weaponOk} ok, {weaponErr} 断裂");

            // 3. 建造物材料
            var buildGuids = AssetDatabase.FindAssets("t:BuildableData");
            int buildOk = 0, buildErr = 0;
            foreach (var guid in buildGuids)
            {
                var b = AssetDatabase.LoadAssetAtPath<BuildableData>(AssetDatabase.GUIDToAssetPath(guid));
                if (b == null || b.materials == null) continue;
                bool ok = true;
                foreach (var m in b.materials)
                    if (m.itemData == null) { Debug.LogError($"❌ 建造材料缺失: {b.displayName} ← null"); ok = false; buildErr++; errors++; }
                if (ok) buildOk++;
            }
            Debug.Log($"  ✅ 建造物: {buildOk} ok, {buildErr} 断裂");

            // 4. 生产设备
            var devGuids = AssetDatabase.FindAssets("t:ProductionDeviceData");
            int devOk = 0, devErr = 0;
            foreach (var guid in devGuids)
            {
                var d = AssetDatabase.LoadAssetAtPath<ProductionDeviceData>(AssetDatabase.GUIDToAssetPath(guid));
                if (d == null || d.recipes == null) continue;
                bool ok = true;
                foreach (var r in d.recipes)
                {
                    if (r.output == null) { Debug.LogError($"❌ 设备产出空: {d.deviceName}"); ok = false; devErr++; errors++; }
                }
                if (d.fuelItem == null && d.requiresFuel) { Debug.LogWarning($"⚠️ 设备燃料空: {d.deviceName} (requiresFuel=true)"); }
                if (ok) devOk++;
            }
            Debug.Log($"  ✅ 生产设备: {devOk} ok, {devErr} 断裂");

            Debug.Log(errors > 0
                ? $"[ItemGraph] ⚠️ 引用完整性: {errors} 个断裂"
                : "[ItemGraph] ✅ 引用完整性: 全部有效");
        }

        /// <summary>
        /// 自动检测链类型（基于 ItemCategory + 名称关键词）
        /// 检测顺序: Electronics > Chemical > Energy > Biological > Food > Metal
        /// </summary>
        private static ChainType DetectChainType(string itemName, ItemData itemData)
        {
            // ---- Electronics 关键词 ----
            if (ContainsAny(itemName,
                "电路", "芯片", "电容", "线圈", "传感器", "电池", "光学", "伺服", "电子",
                "夜视", "消音", "遥控", "雷达", "信号", "电磁", "炮塔", "无人机", "卫星",
                "气象", "环境监测", "探测", "密码", "编程", "自动门", "AI", "人工智能",
                "手电", "手表", "指南针", "打火机", "太阳能", "广播", "通讯", "精密",
                "基因分析", "数据", "终端", "解码", "自动化", "智能", "电线", "电缆",
                "扳手", "螺丝刀", "钥匙", "蓝图"))
                return ChainType.Electronics;

            // ---- Chemical 关键词（必须在Biological之前——"火药"含"药"） ----
            if (ContainsAny(itemName,
                "汽油", "柴油", "煤焦油", "精炼煤", "酸", "碱", "塑料", "橡胶", "火药",
                "炸药", "硫磺", "硝石", "化学", "焦油", "燃烧弹", "闪光弹", "烟雾弹",
                "手榴弹", "燃烧瓶", "爆炸", "酒精", "化肥", "净化水", "消毒水", "止血",
                "解毒", "维生素", "抗生素", "固体酒精", "蒸馏", "电解", "发酵", "盐",
                "合成汽油", "化学试剂", "硫酸", "玻璃", "碳纤维", "无烟火药", "黑火药",
                "吗啡", "肾上腺素", "器官修复", "神经再生", "免疫增强",
                "防毒", "面具", "止痛", "制药"))
                return ChainType.Chemical;

            // ---- Energy 关键词 ----
            if (ContainsAny(itemName,
                "浓缩铀", "聚变", "等离", "超导", "铀", "反应堆", "核电站", "核",
                "能源核心", "聚变核心"))
                return ChainType.Energy;

            // ---- Biological 关键词 ----
            if (ContainsAny(itemName,
                "兽皮", "兽骨", "皮革", "植物纤维", "树皮", "草药", "花", "根", "叶",
                "蘑菇", "菌", "基因", "酶", "激素", "种", "羽毛", "树枝", "原木",
                "背包", "夹克", "背心", "腰封", "裤", "帽", "牛仔",
                "绷带", "手术", "急救", "注射", "免疫", "夹板", "T恤", "布匹",
                "碎布料", "绳索", "绳子", "织", "缝", "布", "皮", "纤", "木",
                "石地板", "石墙", "木地板", "木墙", "木棍", "木盾",
                "线"))
                return ChainType.Biological;

            // ---- Food 关键词 ----
            if (ContainsAny(itemName,
                "肉干", "烤肉", "烤鱼", "烤蘑菇", "炖菜", "汤", "果酱", "罐头", "腌菜",
                "面包", "米饭", "面条", "酒", "果汁", "咖啡", "威士忌", "煮蛋",
                "熏肉", "熏制", "奶", "蛋", "麦", "米", "玉米", "土豆", "番茄",
                "水果", "蔬菜", "鱼", "肉", "菜", "浆果", "浆", "果", "能量棒",
                "生肉", "饮用水", "食用"))
                return ChainType.Food;

            // ---- Metal 关键词（金属零件/武器/护甲/载具） ----
            if (ContainsAny(itemName,
                "锭", "零件", "弹簧", "齿轮", "轴承", "钢管", "钢筋", "合金",
                "钛", "铝", "钢", "铁", "铜", "螺丝", "钉", "矿", "石",
                "枪", "剑", "刀", "斧", "盾", "甲", "头盔", "弓", "弩", "棍", "锤",
                "车", "引擎", "车门", "轮胎", "弹头", "弹壳", "底火",
                "工具", "扳手", "棒球"))
                return ChainType.Metal;

            // 全名兜底：按 ItemCategory 给默认值
            if (itemData != null)
            {
                switch (itemData.category)
                {
                    case ItemCategory.Consumable:
                        return ChainType.Biological;
                    case ItemCategory.Ammo:
                        return ChainType.Metal;
                    case ItemCategory.Equipment:
                        return ChainType.Metal;
                    case ItemCategory.SemiFinished:
                        return ChainType.Metal;
                    case ItemCategory.RawMaterial:
                        return ChainType.Metal;
                    case ItemCategory.Functional:
                        return ChainType.Electronics;
                }
            }

            return ChainType.Metal;
        }

        private static bool ContainsAny(string text, params string[] keywords)
        {
            foreach (var kw in keywords)
                if (text.Contains(kw))
                    return true;
            return false;
        }

        /// <summary>
        /// 规则驱动的工作台自动分配：链类型 × 深度 → WorkstationTier
        /// </summary>
        public static WorkstationTier GetAutoStationStatic(ChainType chain, int minDepth)
        {
            switch (chain)
            {
                case ChainType.Metal:
                    if (minDepth <= 0) return WorkstationTier.Hands;
                    if (minDepth == 1) return WorkstationTier.Furnace;
                    if (minDepth <= 3) return WorkstationTier.MediumBench;
                    return WorkstationTier.Machining;

                case ChainType.Electronics:
                    if (minDepth <= 0) return WorkstationTier.Hands;
                    if (minDepth <= 2) return WorkstationTier.Furnace;
                    if (minDepth <= 4) return WorkstationTier.AdvancedBench;
                    return WorkstationTier.ElectronicsAssembly;

                case ChainType.Chemical:
                    if (minDepth <= 0) return WorkstationTier.Hands;
                    if (minDepth == 1) return WorkstationTier.Furnace;
                    if (minDepth <= 3) return WorkstationTier.MediumBench;
                    if (minDepth <= 6) return WorkstationTier.Chemistry;
                    return WorkstationTier.ElementFurnace;

                case ChainType.Biological:
                    if (minDepth <= 0) return WorkstationTier.Hands;
                    if (minDepth <= 3) return WorkstationTier.MediumBench;
                    if (minDepth <= 5) return WorkstationTier.AdvancedBench;
                    if (minDepth <= 7) return WorkstationTier.Chemistry;
                    return WorkstationTier.ElectronicsAssembly; // 基因分析台

                case ChainType.Food:
                    if (minDepth <= 0) return WorkstationTier.Hands;
                    if (minDepth == 1) return WorkstationTier.Campfire;
                    return WorkstationTier.MediumBench;

                case ChainType.Energy:
                    if (minDepth <= 0) return WorkstationTier.Hands;
                    if (minDepth <= 3) return WorkstationTier.Machining;
                    if (minDepth <= 6) return WorkstationTier.ElectronicsAssembly;
                    return WorkstationTier.ElementFurnace;

                default:
                    return WorkstationTier.Hands;
            }
        }

        /// <summary>
        /// 输出诊断报告到 Console
        /// </summary>
        private static void PrintReport(List<ItemGraphNode> nodes, int rawMatCount, int deadEndCount, int coreCount)
        {
            Debug.Log("========== ItemGraph 诊断报告 ==========");
            Debug.Log($"总物品: {nodes.Count}  原材料: {rawMatCount}  断头路: {deadEndCount}  核心材料: {coreCount}");

            // 断头路清单
            var deadEnds = nodes.Where(n => n.isDeadEnd && !n.isRawMaterial).OrderByDescending(n => n.consumerCount).ToArray();
            if (deadEnds.Length > 0)
            {
                Debug.Log($"\n--- 断头路 ({deadEnds.Length} 个，排除原材料) ---");
                foreach (var n in deadEnds)
                    Debug.Log($"  [{n.primaryChain}] {n.itemName}  depth={n.MinDepth}  下游=0");
            }

            // 核心材料（热度排名）
            var coreMats = nodes.Where(n => n.consumerCount >= 5).OrderByDescending(n => n.consumerCount).ToArray();
            if (coreMats.Length > 0)
            {
                Debug.Log($"\n--- 核心材料 (热度≥5，共 {coreMats.Length} 个) ---");
                foreach (var n in coreMats)
                    Debug.Log($"  [{n.primaryChain}] {n.itemName}  热度={n.consumerCount}  depth={n.MinDepth}  station={n.EffectiveStation}");
            }

            // 按工作台统计
            Debug.Log("\n--- 工作台分配统计 ---");
            var byStation = nodes.GroupBy(n => n.EffectiveStation)
                .OrderBy(g => g.Key)
                .ToDictionary(g => g.Key, g => g.Count());
            foreach (var kv in byStation)
                Debug.Log($"  {kv.Key}: {kv.Value} 个物品");

            // 按链类型统计
            Debug.Log("\n--- 链类型统计 ---");
            var byChain = nodes.GroupBy(n => n.primaryChain)
                .OrderByDescending(g => g.Count())
                .ToDictionary(g => g.Key, g => g.Count());
            foreach (var kv in byChain)
                Debug.Log($"  {kv.Key}: {kv.Value} 个物品");

            Debug.Log("========== 报告结束 ==========");
        }

        /// <summary>
        /// 遍历所有 RecipeData，根据图谱节点的 effectiveStation 同步 requiredStation
        /// </summary>
        private static int SyncRecipeStations(List<ItemGraphNode> nodes)
        {
            var stationLookup = new Dictionary<string, WorkstationTier>();
            foreach (var node in nodes)
                stationLookup[node.itemName] = node.EffectiveStation;

            var recipeGuids = AssetDatabase.FindAssets("t:RecipeData");
            int updated = 0;

            foreach (var guid in recipeGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var recipe = AssetDatabase.LoadAssetAtPath<RecipeData>(path);
                if (recipe == null || recipe.resultItem == null) continue;

                var itemName = recipe.resultItem.itemName;
                if (string.IsNullOrEmpty(itemName)) continue;

                if (!stationLookup.TryGetValue(itemName, out var targetStation))
                {
                    Debug.LogWarning($"[ItemGraphBuilder] 配方 {recipe.recipeName} 的产出 {itemName} 不在图谱中，跳过同步");
                    continue;
                }

                if (recipe.requiredStation != targetStation)
                {
                    var oldStation = recipe.requiredStation;
                    Undo.RecordObject(recipe, "同步工作台等级");
                    recipe.requiredStation = targetStation;
                    EditorUtility.SetDirty(recipe);
                    updated++;
                    Debug.Log($"  [同步] {recipe.recipeName}: {oldStation} → {targetStation}");
                }
            }

            if (updated > 0)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log($"[ItemGraphBuilder] 配方同步完成，更新 {updated}/{recipeGuids.Length} 个配方");
            }
            else
            {
                Debug.Log($"[ItemGraphBuilder] 配方同步：所有 {recipeGuids.Length} 个配方已是最新，无需更新");
            }

            return updated;
        }
    }
}
