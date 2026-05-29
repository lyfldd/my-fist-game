using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using _Game.Config;

/// <summary>
/// 一键创建工业设备 — 自动生成 ProductionDeviceData + RecipeData + BuildableData + ItemData + 更新Catalog
/// 菜单栏 → Tools/工业/一键创建工业设备
/// </summary>
public class CreateIndustrialDeviceEditor : EditorWindow
{
    // 设备基本信息
    string _deviceName = "";
    string _assetName = "";
    ChainType _chain = ChainType.Metal;
    WorkstationTier _tier = WorkstationTier.Machining;
    float _interval = 5f;
    int _batchSize = 1;
    bool _requiresFuel;
    string _fuelItemName = "Coal";
    float _fuelPerCycle = 1f;
    bool _acceptsAutomation = true;

    // 配方
    List<RecipeEntry> _recipes = new List<RecipeEntry>();

    // 建造
    int _buildSkillLevel = 2;
    int _buildDuration = 10;
    float _maxHealth = 150f;
    Vector3 _placementSize = new Vector3(2f, 2f, 2f);
    string _kitDisplayName = "";
    BuildableCategory _autoCategory;

    // 材料
    List<MaterialEntry> _kitMaterials = new List<MaterialEntry>();

    // 其他
    string _description = "";
    Vector2 _scroll;
    bool _foldoutRecipes = true;
    bool _foldoutBuild = true;

    [System.Serializable]
    class RecipeEntry
    {
        public string inputName;
        public int inputCount = 1;
        public string outputName;
        public int outputCount = 1;
        public float baseTime = 5f;
    }

    [System.Serializable]
    class MaterialEntry
    {
        public string itemName;
        public int count = 1;
    }

    static Dictionary<string, ItemData> _itemLookup;

    [MenuItem("Tools/工业/一键创建工业设备")]
    public static void ShowWindow()
    {
        GetWindow<CreateIndustrialDeviceEditor>("工业设备创建器");
    }

    void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        GUILayout.Label("一键创建工业设备", EditorStyles.boldLabel);
        GUILayout.Space(4);

        // 基本
        EditorGUILayout.LabelField("设备基本信息", EditorStyles.boldLabel);
        _assetName = EditorGUILayout.TextField("资产名(英文)", _assetName);
        _deviceName = EditorGUILayout.TextField("设备名(中文)", _deviceName);
        _chain = (ChainType)EditorGUILayout.EnumPopup("产业链", _chain);
        _tier = (WorkstationTier)EditorGUILayout.EnumPopup("设备等级", _tier);
        _interval = EditorGUILayout.FloatField("生产间隔(秒)", _interval);
        _batchSize = EditorGUILayout.IntField("批次数", _batchSize);
        _requiresFuel = EditorGUILayout.Toggle("需要燃料", _requiresFuel);
        if (_requiresFuel)
        {
            _fuelItemName = EditorGUILayout.TextField("燃料物品名", _fuelItemName);
            _fuelPerCycle = EditorGUILayout.FloatField("每次燃料消耗", _fuelPerCycle);
        }
        _acceptsAutomation = EditorGUILayout.Toggle("支持自动化", _acceptsAutomation);
        GUILayout.Space(8);

        // 配方
        _foldoutRecipes = EditorGUILayout.Foldout(_foldoutRecipes, $"生产配方 ({_recipes.Count})", true);
        if (_foldoutRecipes)
        {
            for (int i = 0; i < _recipes.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                _recipes[i].inputName = EditorGUILayout.TextField(_recipes[i].inputName, GUILayout.Width(100));
                EditorGUILayout.LabelField("×", GUILayout.Width(12));
                _recipes[i].inputCount = EditorGUILayout.IntField(_recipes[i].inputCount, GUILayout.Width(40));
                EditorGUILayout.LabelField("→", GUILayout.Width(16));
                _recipes[i].outputName = EditorGUILayout.TextField(_recipes[i].outputName, GUILayout.Width(100));
                EditorGUILayout.LabelField("×", GUILayout.Width(12));
                _recipes[i].outputCount = EditorGUILayout.IntField(_recipes[i].outputCount, GUILayout.Width(40));
                _recipes[i].baseTime = EditorGUILayout.FloatField(_recipes[i].baseTime, GUILayout.Width(40));
                if (GUILayout.Button("×", GUILayout.Width(22)))
                    _recipes.RemoveAt(i--);
                EditorGUILayout.EndHorizontal();
            }
            if (GUILayout.Button("+ 添加配方"))
                _recipes.Add(new RecipeEntry());
        }
        GUILayout.Space(8);

        // 建造
        _foldoutBuild = EditorGUILayout.Foldout(_foldoutBuild, "建造数据", true);
        if (_foldoutBuild)
        {
            _buildSkillLevel = EditorGUILayout.IntField("建造技能等级", _buildSkillLevel);
            _buildDuration = EditorGUILayout.IntField("建造耗时(秒)", _buildDuration);
            _maxHealth = EditorGUILayout.FloatField("血量", _maxHealth);
            _placementSize = EditorGUILayout.Vector3Field("占地尺寸", _placementSize);
            _kitDisplayName = EditorGUILayout.TextField("套件显示名(留空=设备名+套件)", _kitDisplayName);
            _autoCategory = GetAutoCategory();

            EditorGUILayout.LabelField($"自动分类: {CategoryLabel(_autoCategory)}", EditorStyles.miniLabel);

            // 套件材料
            EditorGUILayout.LabelField("套件建造材料:");
            for (int i = 0; i < _kitMaterials.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                _kitMaterials[i].itemName = EditorGUILayout.TextField(_kitMaterials[i].itemName, GUILayout.Width(120));
                EditorGUILayout.LabelField("×", GUILayout.Width(12));
                _kitMaterials[i].count = EditorGUILayout.IntField(_kitMaterials[i].count, GUILayout.Width(50));
                if (GUILayout.Button("×", GUILayout.Width(22)))
                    _kitMaterials.RemoveAt(i--);
                EditorGUILayout.EndHorizontal();
            }
            if (GUILayout.Button("+ 添加材料"))
                _kitMaterials.Add(new MaterialEntry());
        }
        GUILayout.Space(8);

        // 描述
        _description = EditorGUILayout.TextField("设备描述", _description);

        GUILayout.Space(12);

        // 生成按钮
        GUI.backgroundColor = new Color(0.3f, 0.7f, 0.3f);
        if (GUILayout.Button("一键生成全部资产", GUILayout.Height(30)))
        {
            if (string.IsNullOrEmpty(_assetName) || string.IsNullOrEmpty(_deviceName))
            {
                EditorUtility.DisplayDialog("错误", "请填写设备资产名和显示名", "确定");
                return;
            }
            if (_recipes.Count == 0)
            {
                EditorUtility.DisplayDialog("错误", "至少需要一个生产配方", "确定");
                return;
            }
            CreateAll();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndScrollView();
    }

    BuildableCategory GetAutoCategory()
    {
        return _chain switch
        {
            ChainType.Metal => BuildableCategory.MetalIndustry,
            ChainType.Electronics => BuildableCategory.ElectronicsIndustry,
            ChainType.Chemical => BuildableCategory.ChemicalIndustry,
            ChainType.Biological => BuildableCategory.BioIndustry,
            ChainType.Food => BuildableCategory.BioIndustry,
            ChainType.Energy => BuildableCategory.EnergyIndustry,
            _ => BuildableCategory.MetalIndustry,
        };
    }

    string CategoryLabel(BuildableCategory cat) => cat switch
    {
        BuildableCategory.MetalIndustry => "金属工业",
        BuildableCategory.ElectronicsIndustry => "电子工业",
        BuildableCategory.ChemicalIndustry => "化学工业",
        BuildableCategory.BioIndustry => "生物食品工业",
        BuildableCategory.EnergyIndustry => "能源工业",
        _ => cat.ToString(),
    };

    void BuildItemLookup()
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

    ItemData GetItem(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        if (_itemLookup.TryGetValue(name, out var item)) return item;
        Debug.LogWarning($"[CreateIndustrialDevice] 未找到 ItemData: {name}");
        return null;
    }

    void CreateAll()
    {
        BuildItemLookup();

        string kitName = string.IsNullOrEmpty(_kitDisplayName) ? $"{_deviceName}套件" : _kitDisplayName;
        string kitAssetName = "WS_" + _assetName;

        // 1. ProductionDeviceData
        string prodDir = "Assets/_Game/Config/ProductionDevices";
        if (!AssetDatabase.IsValidFolder(prodDir))
            AssetDatabase.CreateFolder("Assets/_Game/Config", "ProductionDevices");

        var pd = ScriptableObject.CreateInstance<ProductionDeviceData>();
        pd.deviceName = _deviceName;
        pd.tier = _tier;
        pd.productionInterval = _interval;
        pd.batchSize = _batchSize;
        pd.requiresFuel = _requiresFuel;
        pd.fuelItem = _requiresFuel ? GetItem(_fuelItemName) : null;
        pd.fuelPerCycle = _fuelPerCycle;
        pd.acceptsAutomation = _acceptsAutomation;

        var prodRecipes = new ProductionRecipe[_recipes.Count];
        for (int i = 0; i < _recipes.Count; i++)
        {
            prodRecipes[i] = new ProductionRecipe
            {
                input = GetItem(_recipes[i].inputName),
                inputCount = _recipes[i].inputCount,
                output = GetItem(_recipes[i].outputName),
                outputCount = _recipes[i].outputCount,
                baseTime = _recipes[i].baseTime,
            };
        }
        pd.recipes = prodRecipes;

        string pdPath = $"{prodDir}/{_assetName}.asset";
        var existingPd = AssetDatabase.LoadAssetAtPath<ProductionDeviceData>(pdPath);
        if (existingPd != null) AssetDatabase.DeleteAsset(pdPath);
        AssetDatabase.CreateAsset(pd, pdPath);

        // 2. 工业 RecipeData .assets
        string recipeDir = $"Assets/_Game/Config/Recipes/工业/{_deviceName}";
        if (!AssetDatabase.IsValidFolder(recipeDir))
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Game/Config/Recipes/工业"))
                AssetDatabase.CreateFolder("Assets/_Game/Config/Recipes", "工业");
            AssetDatabase.CreateFolder("Assets/_Game/Config/Recipes/工业", _deviceName);
        }

        for (int i = 0; i < _recipes.Count; i++)
        {
            var rd = ScriptableObject.CreateInstance<RecipeData>();
            rd.recipeName = $"{_deviceName}_{_recipes[i].outputName}";
            rd.requiredStation = _tier;
            rd.isIndustrial = true;
            rd.productionDeviceName = _deviceName;
            rd.resultItem = GetItem(_recipes[i].outputName);
            rd.resultCount = _recipes[i].outputCount;
            rd.craftTime = _recipes[i].baseTime;

            var mats = new List<ItemRequirement>();
            if (!string.IsNullOrEmpty(_recipes[i].inputName) && _recipes[i].inputCount > 0)
            {
                mats.Add(new ItemRequirement
                {
                    itemData = GetItem(_recipes[i].inputName),
                    count = _recipes[i].inputCount,
                });
            }
            rd.materials = mats.ToArray();

            string rdPath = $"{recipeDir}/{_deviceName}_{_recipes[i].outputName}.asset";
            AssetDatabase.CreateAsset(rd, rdPath);
        }

        // 3. 套件 ItemData (如果不存在)
        string itemDir = "Assets/_Game/Config/Items/Industrial";
        if (!AssetDatabase.IsValidFolder(itemDir))
            AssetDatabase.CreateFolder("Assets/_Game/Config/Items", "Industrial");

        string itemPath = $"{itemDir}/{kitAssetName}.asset";
        ItemData kitItem = AssetDatabase.LoadAssetAtPath<ItemData>(itemPath);
        if (kitItem == null)
        {
            kitItem = ScriptableObject.CreateInstance<ItemData>();
            kitItem.itemName = kitName;
            kitItem.category = ItemCategory.SemiFinished;
            kitItem.maxStack = 1;
            kitItem.weight = 0.5f;
            kitItem.description = $"{_deviceName}的组装套件，用于建造{_deviceName}。";
            AssetDatabase.CreateAsset(kitItem, itemPath);
        }

        // 4. 套件 RecipeData (手工配方)
        string kitRecipeDir = $"Assets/_Game/Config/Recipes/高级工作台";
        if (!AssetDatabase.IsValidFolder(kitRecipeDir))
            AssetDatabase.CreateFolder("Assets/_Game/Config/Recipes", "高级工作台");

        var kitRecipe = ScriptableObject.CreateInstance<RecipeData>();
        kitRecipe.recipeName = kitName;
        kitRecipe.requiredStation = WorkstationTier.AdvancedBench;
        kitRecipe.resultItem = kitItem;
        kitRecipe.resultCount = 1;
        kitRecipe.craftTime = 8f;

        var kitMats = new List<ItemRequirement>();
        foreach (var m in _kitMaterials)
        {
            var item = GetItem(m.itemName);
            if (item != null)
            {
                kitMats.Add(new ItemRequirement { itemData = item, count = m.count });
            }
        }
        kitRecipe.materials = kitMats.ToArray();

        string kitRecipePath = $"{kitRecipeDir}/{kitAssetName}.asset";
        AssetDatabase.CreateAsset(kitRecipe, kitRecipePath);

        // 5. BuildableData
        string buildDir = "Assets/_Game/Config/BuildableData";
        if (!AssetDatabase.IsValidFolder(buildDir))
            AssetDatabase.CreateFolder("Assets/_Game/Config", "BuildableData");

        var bd = ScriptableObject.CreateInstance<BuildableData>();
        bd.displayName = _deviceName;
        bd.description = _description;
        bd.category = _autoCategory;
        bd.buildDuration = _buildDuration;
        bd.maxHealth = _maxHealth;
        bd.snapSize = 1f;
        bd.placementSize = _placementSize;
        bd.isWorkstation = false;
        bd.industrialChain = _chain;
        bd.productionDeviceRef = pd;

        if (_buildSkillLevel > 0)
        {
            bd.skillRequirements = new SkillRequirement[]
            {
                new SkillRequirement { skill = SkillType.建造拆解, level = _buildSkillLevel },
            };
        }

        var buildMats = new List<ItemRequirement>();
        buildMats.Add(new ItemRequirement { itemData = kitItem, count = 1 });
        foreach (var m in _kitMaterials)
        {
            var item = GetItem(m.itemName);
            if (item != null)
                buildMats.Add(new ItemRequirement { itemData = item, count = m.count });
        }
        bd.materials = buildMats.ToArray();

        // 产出预览
        var produced = new List<string>();
        foreach (var r in _recipes)
            if (!string.IsNullOrEmpty(r.outputName))
                produced.Add(r.outputName);
        bd.producedItems = produced.ToArray();

        string bdPath = $"{buildDir}/Buildable_{_assetName}.asset";
        AssetDatabase.CreateAsset(bd, bdPath);

        // 6. 更新 Catalog
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 提示重建BuildableCatalog
        Debug.Log($"[CreateIndustrialDevice] 设备 '{_deviceName}' 全部资产已生成！");
        Debug.Log($"  ProductionDeviceData: {pdPath}");
        Debug.Log($"  RecipeData(工业): {recipeDir} ({_recipes.Count}个)");
        Debug.Log($"  ItemData(套件): {itemPath}");
        Debug.Log($"  RecipeData(套件): {kitRecipePath}");
        Debug.Log($"  BuildableData: {bdPath}");
        Debug.Log($"  ⚠ 请运行 'Tools/工业/重建 BuildableCatalog' 和 'Tools/工业/重建 RecipeCatalog' 以更新目录。");

        EditorUtility.DisplayDialog("生成完毕",
            $"设备 '{_deviceName}' 全部资产已生成！\n\n" +
            $"ProductionDeviceData: 1\nRecipeData(工业): {_recipes.Count}\n" +
            $"ItemData(套件): 1\nRecipeData(套件): 1\nBuildableData: 1\n\n" +
            $"⚠ 请手动运行:\n1. Game Tools/Create Default Buildables\n2. Tools/工业/生成工业 RecipeData",
            "确定");
    }
}
