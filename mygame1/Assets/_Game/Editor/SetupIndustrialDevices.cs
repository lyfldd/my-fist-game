using UnityEditor;
using UnityEngine;
using _Game.Config;
using _Game.Systems.Crafting;
using _Game.Systems.Inventory;

/// <summary>
/// 一键生成工业设备预制体并关联所有引用。
/// 菜单栏 → Game Tools → Setup Industrial Devices
///
/// 自动完成:
///   1. 为 19 个工业设备创建预制体 (ProductionDevice + BoxCollider + InputSlot/OutputSlot)
///   2. 设置 ProductionDeviceData.builtPrefab
///   3. 设置 BuildableData.productionDeviceRef
/// </summary>
public static class SetupIndustrialDevices
{
    const string PrefabDir = "Assets/_Game/Prefabs/ProductionDevices";

    // 19 个工业设备的映射: 设备数据名 → 建造物数据名
    static readonly (string deviceDataName, string buildableName)[] Devices =
    {
        ("PressMachine",         "Buildable_PressMachine"),
        ("IndustrialFurnace",    "Buildable_IndustrialFurnace"),
        ("ElectrolysisTank",     "Buildable_ElectrolysisTank"),
        ("Fermenter",            "Buildable_Fermenter"),
        ("Distiller",            "Buildable_Distiller"),
        ("Crusher",              "Buildable_Crusher"),
        ("Loom",                 "Buildable_Loom"),
        ("Sawmill",              "Buildable_Sawmill"),
        ("WaterPump",            "Buildable_WaterPump"),
        ("Kiln",                 "Buildable_Kiln"),
        ("Lathe",                "Buildable_Lathe"),
        ("AssemblyTable",        "Buildable_AssemblyTable"),
        ("RecyclingStation",     "Buildable_RecyclingStation"),
        ("Smokehouse",           "Buildable_Smokehouse"),
        ("CanningMachine",       "Buildable_CanningMachine"),
        ("PharmaBench",          "Buildable_PharmaBench"),
        ("RadioTower",           "Buildable_RadioTower"),

        // 终局设备
        ("Centrifuge",           "Buildable_Centrifuge"),
        ("GeneAnalyzer",         "Buildable_GeneAnalyzer"),
        ("PrecisionAssembly",    "Buildable_PrecisionAssembly"),
        ("WaterPurifier",        "Buildable_WaterPurifier"),
        ("AIBot",                "Buildable_AIBot"),
        ("NuclearPlant",         "Buildable_NuclearPlant"),
    };

    [MenuItem("Game Tools/Setup Industrial Devices")]
    public static void SetupAll()
    {
        EnsureDirectory();

        int done = 0;
        foreach (var (devName, buildableName) in Devices)
        {
            if (SetupDevice(devName, buildableName))
                done++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[SetupIndustrialDevices] 完成！{done}/{Devices.Length} 个设备已设置。");
    }

    static bool SetupDevice(string deviceDataName, string buildableName)
    {
        // 1. 加载 ProductionDeviceData
        string devPath = $"Assets/_Game/Config/ProductionDevices/{deviceDataName}.asset";
        var deviceData = AssetDatabase.LoadAssetAtPath<ProductionDeviceData>(devPath);
        if (deviceData == null)
        {
            Debug.LogError($"未找到 ProductionDeviceData: {devPath}");
            return false;
        }

        // 2. 创建或更新预制体
        string prefabPath = $"{PrefabDir}/DEV_{deviceDataName}.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

        if (prefab == null)
        {
            // 新建预制体
            var go = new GameObject($"DEV_{deviceDataName}");
            SetupGameObject(go, deviceData);
            prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            Object.DestroyImmediate(go);
            Debug.Log($"创建预制体: {prefabPath}");
        }
        else
        {
            // 更新已有预制体
            var contents = PrefabUtility.LoadPrefabContents(prefabPath);
            SetupGameObject(contents, deviceData);
            PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            PrefabUtility.UnloadPrefabContents(contents);
            Debug.Log($"更新预制体: {prefabPath}");
        }

        // 3. 设置 ProductionDeviceData.builtPrefab
        var devSo = new SerializedObject(deviceData);
        devSo.FindProperty("builtPrefab").objectReferenceValue = prefab;
        devSo.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(deviceData);

        // 4. 设置 BuildableData.productionDeviceRef
        string buildablePath = $"Assets/_Game/Config/BuildableData/{buildableName}.asset";
        var buildableData = AssetDatabase.LoadAssetAtPath<BuildableData>(buildablePath);
        if (buildableData != null)
        {
            var bdSo = new SerializedObject(buildableData);
            bdSo.FindProperty("productionDeviceRef").objectReferenceValue = deviceData;
            bdSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(buildableData);
        }
        else
        {
            Debug.LogWarning($"未找到 BuildableData: {buildablePath}");
        }

        return true;
    }

    static System.Reflection.FieldInfo _inputField, _outputField;

    static void SetupGameObject(GameObject go, ProductionDeviceData deviceData)
    {
        // 清理旧组件
        var existingPd = go.GetComponent<ProductionDevice>();
        if (existingPd != null) Object.DestroyImmediate(existingPd);
        var existingCol = go.GetComponent<BoxCollider>();
        if (existingCol != null) Object.DestroyImmediate(existingCol);

        // BoxCollider
        var col = go.AddComponent<BoxCollider>();
        col.size = new Vector3(1.5f, 1.5f, 1.5f);
        col.center = new Vector3(0f, 0.75f, 0f);

        // ProductionDevice — 用 SerializedObject 设置 _data（Unity Object 引用）
        var pd = go.AddComponent<ProductionDevice>();
        var pdSo = new SerializedObject(pd);
        pdSo.FindProperty("_data").objectReferenceValue = deviceData;
        pdSo.ApplyModifiedPropertiesWithoutUndo();

        // InventoryContainer 是普通 [Serializable] 类，用反射直接赋值
        if (_inputField == null)
            _inputField = typeof(ProductionDevice).GetField("_inputSlot",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (_outputField == null)
            _outputField = typeof(ProductionDevice).GetField("_outputSlot",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        _inputField?.SetValue(pd, new InventoryContainer
        {
            containerName = $"{deviceData.deviceName} 输入槽",
            gridWidth = 4,
            gridHeight = 3
        });

        _outputField?.SetValue(pd, new InventoryContainer
        {
            containerName = $"{deviceData.deviceName} 输出槽",
            gridWidth = 4,
            gridHeight = 2
        });

        // 强制 Unity 重新序列化整个组件（包括嵌套的 InventoryContainer）
        EditorUtility.SetDirty(pd);
    }

    static void EnsureDirectory()
    {
        if (!AssetDatabase.IsValidFolder("Assets/_Game/Prefabs"))
            AssetDatabase.CreateFolder("Assets/_Game", "Prefabs");
        if (!AssetDatabase.IsValidFolder(PrefabDir))
            AssetDatabase.CreateFolder("Assets/_Game/Prefabs", "ProductionDevices");
    }
}
