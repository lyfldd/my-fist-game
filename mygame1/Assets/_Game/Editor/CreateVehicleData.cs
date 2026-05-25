using UnityEditor;
using UnityEngine;
using _Game.Config;

/// <summary>
/// 编辑器工具：创建默认车辆数据 + 快速搭建车辆 Prefab。
///
/// v7 — 轮子旋转退化为本地空间计算
///   ★ 问题根源 v6：LookRotation(worldForward, Vector3.up) 在 X=-90° 时退化
///     worldForward=世界Y=Vector3.up → forward==up → LookRotation 返回垃圾(Y=90°)
///   ★ 修复：本地空间直接计算 wheelRot
///     localSuspension=Inv(rootRot)*worldUp, localAxle=rightAxis, localRoll=cross(axle,susp)
///
/// 历史：
/// v6 — 世界空间位置计算（仍保留位置部分，旋转改本地）
/// v5 — 彻底修复轮子位置/旋转残留（旧对象复用导致数据不一致）
/// v4 — 首次引入缩放补偿
///
/// 用法：Unity 菜单 → Tools/Vehicles/...
/// </summary>
public class CreateVehicleData
{
    // ---- 模型包围盒计算结果 ----
    private struct BoundsInfo
    {
        public Vector3 center;       // 在根物体本地空间
        public Vector3 size;
        public Vector3 min;
        public Vector3 max;

        // 自动检测的轴向（均为本地空间单位向量）
        public Vector3 forwardAxis;  // 车头朝向（水平长轴）
        public Vector3 rightAxis;    // 车身右侧（水平短轴）
        public Vector3 upAxis;       // 车顶方向（世界 Y）

        public float carLength;      // 沿 forwardAxis 的尺寸
        public float carWidth;       // 沿 rightAxis 的尺寸
        public float carHeight;      // 沿 upAxis 的尺寸

        // 底部平面（4 个角点，用于放轮子）
        public float bottomY;        // 车身最低点 Y
    }

    // ============================================================
    // 菜单：创建 VehicleData
    // ============================================================

    [MenuItem("Tools/Vehicles/Create OffRoad Vehicle Data")]
    public static void CreateOffRoadData()
    {
        string dir = "Assets/_Game/Config/VehicleData";
        if (!AssetDatabase.IsValidFolder(dir))
            AssetDatabase.CreateFolder("Assets/_Game/Config", "VehicleData");

        string path = dir + "/OffRoad.asset";

        var existing = AssetDatabase.LoadAssetAtPath<VehicleData>(path);
        if (existing != null)
        {
            Debug.LogWarning($"VehicleData 已存在: {path}，跳过创建。");
            return;
        }

        var data = ScriptableObject.CreateInstance<VehicleData>();
        data.vehicleName = "OffRoad 越野车";
        data.category = VehicleCategory.Car;
        data.maxSpeed = 12f;
        data.motorTorque = 1000f;
        data.brakingForce = 3000f;
        data.maxSteerAngle = 35f;
        data.reverseTorque = 500f;
        data.mass = 1800f;
        data.suspensionDamper = 8000f;
        data.fuelCapacity = 50f;
        data.fuelConsumptionRate = 0.03f;
        data.trunkWidth = 4;
        data.trunkHeight = 3;

        // 这些会在 Setup 时被模型包围盒覆盖
        data.driverSeatOffset = new Vector3(-0.6f, 1.2f, 0.3f);
        data.exitOffset = new Vector3(-2.5f, 0f, 0f);
        data.colliderCenter = Vector3.zero;
        data.colliderSize = Vector3.one;

        AssetDatabase.CreateAsset(data, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"已创建 VehicleData: {path}");
    }

    // ============================================================
    // 菜单：Setup Vehicle Prefab（选中模型后执行）
    // ============================================================

    [MenuItem("Tools/Vehicles/Setup Vehicle Prefab (from selected)", true)]
    public static bool SetupVehiclePrefabValidate()
    {
        return Selection.activeGameObject != null;
    }

    [MenuItem("Tools/Vehicles/Setup Vehicle Prefab (from selected)")]
    public static void SetupVehiclePrefab()
    {
        var selected = Selection.activeGameObject;
        if (selected == null)
        {
            Debug.LogError("请先在 Project 窗口中选中车辆模型 Prefab！");
            return;
        }

        string prefabPath = AssetDatabase.GetAssetPath(selected);
        if (string.IsNullOrEmpty(prefabPath))
        {
            var source = PrefabUtility.GetCorrespondingObjectFromSource(selected);
            if (source != null)
                prefabPath = AssetDatabase.GetAssetPath(source);
        }

        if (string.IsNullOrEmpty(prefabPath))
        {
            Debug.LogError("无法获取选中对象的预制体路径！");
            return;
        }

        SetupPrefabAtPath(prefabPath);
    }

    // ============================================================
    // Quick Setup
    // ============================================================

    [MenuItem("Tools/Vehicles/Quick Setup OffRoad (Data + Prefab)")]
    public static void QuickSetupOffRoad()
    {
        CreateOffRoadData();
        AssetDatabase.Refresh();

        string prefabPath = "Assets/_Game/Models/Vehicles/OffRoad.prefab";
        var loaded = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (loaded != null)
        {
            SetupPrefabAtPath(prefabPath);
        }
        else
        {
            Debug.LogWarning($"未找到 {prefabPath}，请手动搭建。");
        }

        Debug.Log("=== OffRoad 车辆搭建完成！===");
    }

    // ============================================================
    // 核心：基于包围盒搭建 Prefab（v7 — 本地空间旋转修复 LookRotation 退化）
    // ============================================================

    /// <summary>
    /// 目标车长（世界空间，米）。典型越野车约 4.5m。
    /// 若模型以 mm/cm 导出（包围盒仅 ~0.02m），自动缩放根物体并补偿轮子参数。
    /// </summary>
    private const float TARGET_VEHICLE_LENGTH = 4.5f;

    public static void SetupPrefabAtPath(string prefabPath)
    {
        GameObject go = PrefabUtility.LoadPrefabContents(prefabPath);
        if (go == null)
        {
            Debug.LogError($"无法加载预制体: {prefabPath}");
            return;
        }

        // ---- 0. 计算原始包围盒（根物体缩放为 (1,1,1) 时的本地空间尺寸） ----
        BoundsInfo rawBounds = CalculateBounds(go);
        float scaleFactor = TARGET_VEHICLE_LENGTH / Mathf.Max(rawBounds.carLength, 0.001f);

        // ---- 1. 缩放根物体，使模型达到真实世界尺寸 ----
        go.transform.localScale = Vector3.one * scaleFactor;

        // ---- 2. 基于缩放后的世界空间值计算所有参数 ----
        // 原理：CalculateBounds 的矩阵变换会自动抵消根缩放（worldToLocal ÷ scale，
        //       localToWorld × scale），所以 rawBounds 本身就是正确的本地空间值。
        //
        // 我们直接用 rawBounds 计算"世界空间"目标值（× scaleFactor），
        // 然后在设置轮子位置/半径时除以 scaleFactor 得到本地空间值，
        // 最终根缩放会把这些本地空间值乘回正确的世界空间值。
        //
        // 简化后：轮子本地位置 = rawBounds 直接算（与 v3 相同），
        //         轮子本地半径 = 世界目标半径 / scaleFactor

        float wsCarLength = rawBounds.carLength * scaleFactor;
        float wsCarWidth  = rawBounds.carWidth  * scaleFactor;
        float wsCarHeight = rawBounds.carHeight * scaleFactor;
        float wsBottomY   = rawBounds.bottomY  * scaleFactor;

        // 世界空间车轮参数
        float wsWheelRadius       = Mathf.Clamp(wsCarHeight * 0.28f, 0.3f, 0.7f);
        float wsWheelOffsetFwd    = wsCarLength * 0.4f;
        float wsWheelOffsetSide   = wsCarWidth  * 0.425f;
        float wsRoofY             = rawBounds.center.y * scaleFactor + wsCarHeight * 0.5f;

        // 轮子本地半径（= 世界值 ÷ scaleFactor，创建 WheelCollider 时用）
        float localWheelRadius = wsWheelRadius / scaleFactor;
        float invScale         = 1f / scaleFactor;

        // ---- v7 世界空间位置 + 本地空间旋转 ----
        // 位置：世界空间计算 → Inverse 回本地（规避 X=-90° 父旋转扭曲）
        // 旋转：直接在本地空间构建（worldForward=世界Y 时 LookRotation 退化）
        Quaternion rootRot = go.transform.rotation;

        // 本地轴 → 世界空间
        Vector3 worldForward = rootRot * rawBounds.forwardAxis;
        Vector3 worldRight   = rootRot * rawBounds.rightAxis;

        // 世界空间轮子位置
        Vector3 worldFL = worldForward * wsWheelOffsetFwd - worldRight * wsWheelOffsetSide + Vector3.up * wsBottomY;
        Vector3 worldFR = worldForward * wsWheelOffsetFwd + worldRight * wsWheelOffsetSide + Vector3.up * wsBottomY;
        Vector3 worldRL = worldForward * (-wsWheelOffsetFwd) - worldRight * wsWheelOffsetSide + Vector3.up * wsBottomY;
        Vector3 worldRR = worldForward * (-wsWheelOffsetFwd) + worldRight * wsWheelOffsetSide + Vector3.up * wsBottomY;

        // 世界空间 → 本地空间（子物体相对父节点）
        // TransformPoint: worldChild = parentRot * (scale * localChild)
        // → localChild = Inv(parentRot) * worldChild / scale
        Vector3 wheelFLPos = Quaternion.Inverse(rootRot) * (worldFL * invScale);
        Vector3 wheelFRPos = Quaternion.Inverse(rootRot) * (worldFR * invScale);
        Vector3 wheelRLPos = Quaternion.Inverse(rootRot) * (worldRL * invScale);
        Vector3 wheelRRPos = Quaternion.Inverse(rootRot) * (worldRR * invScale);

        // ---- v7 ★ 轮子旋转：本地空间直接计算 ----
        // v6 世界空间 LookRotation(worldForward, Vector3.up) 在 X=-90° 时退化
        //   worldForward = 世界Y = Vector3.up → forward==up → 返回垃圾(Y=90°)
        //
        // 正确做法：在车辆本地空间构建轮子朝向
        //   悬挂(Y轴) = 世界Y 在本地空间的方向 = Inv(rootRot) * Vector3.up
        //   车轴(X轴) = 车身宽度方向(本地) = rawBounds.rightAxis
        //   滚动(Z轴) = cross(车轴, 悬挂)
        Vector3 localSuspension = Quaternion.Inverse(rootRot) * Vector3.up;
        Vector3 localAxle       = rawBounds.rightAxis;
        Vector3 localRoll       = Vector3.Cross(localAxle, localSuspension);
        Quaternion wheelRot     = Quaternion.LookRotation(localRoll, localSuspension);

        Debug.Log($"[Vehicle Setup] === v7 诊断 ===\n" +
                  $"  根节点旋转={rootRot.eulerAngles}  (模型导入旋转)\n" +
                  $"  Raw AABB: size={rawBounds.size:F4} center={rawBounds.center:F4}\n" +
                  $"  Scale factor: {scaleFactor:F2}  (目标车长={TARGET_VEHICLE_LENGTH}m / 原始={rawBounds.carLength:F4}m)\n" +
                  $"  本地轴: fwd={rawBounds.forwardAxis} right={rawBounds.rightAxis}\n" +
                  $"  世界轴: fwd={worldForward:F2} right={worldRight:F2}\n" +
                  $"  世界车长={wsCarLength:F2}m  车宽={wsCarWidth:F2}m  车高={wsCarHeight:F2}m\n" +
                  $"  世界车轮半径={wsWheelRadius:F3}m  本地车轮半径={localWheelRadius:F4}\n" +
                  $"  世界偏移(前后)=±{wsWheelOffsetFwd:F2}m  世界偏移(左右)=±{wsWheelOffsetSide:F2}m\n" +
                  $"  世界底盘Y={wsBottomY:F3}m  车顶Y={wsRoofY:F2}m\n" +
                  $"  世界位置: FL={worldFL:F2} FR={worldFR:F2} RL={worldRL:F2} RR={worldRR:F2}\n" +
                  $"  本地位置: FL={wheelFLPos:F3} FR={wheelFRPos:F3} RL={wheelRLPos:F3} RR={wheelRRPos:F3}\n" +
                  $"  本地悬挂={localSuspension:F3} 本地车轴={localAxle:F3} 本地滚动={localRoll:F3}\n" +
                  $"  轮子旋转(欧拉)={wheelRot.eulerAngles}");

        // ---- 3. Rigidbody ----
        var rb = go.GetComponent<Rigidbody>();
        if (rb == null) rb = go.AddComponent<Rigidbody>();
        rb.mass = 1800f;
        rb.drag = 0.3f;
        rb.angularDrag = 1f;

        // ---- 4. BoxCollider（世界空间值 / scaleFactor → 本地空间） ----
        var bodyCollider = go.GetComponent<BoxCollider>();
        if (bodyCollider == null) bodyCollider = go.AddComponent<BoxCollider>();
        bodyCollider.center = rawBounds.center;
        bodyCollider.size   = rawBounds.size * 0.95f; // 本地空间（与 rawBounds 一致）

        // ---- 5. 强制清除所有旧轮子（名称 + 组件双重检测，不依赖 Find 复用） ----
        DestroyAllWheelChildren(go);

        var wheelFL = CreateWheelChild(go, "WheelFL", wheelFLPos, wheelRot, localWheelRadius);
        var wheelFR = CreateWheelChild(go, "WheelFR", wheelFRPos, wheelRot, localWheelRadius);
        var wheelRL = CreateWheelChild(go, "WheelRL", wheelRLPos, wheelRot, localWheelRadius);
        var wheelRR = CreateWheelChild(go, "WheelRR", wheelRRPos, wheelRot, localWheelRadius);

        // v7 验证：打印每个轮子创建后的确认日志
        Debug.Log($"[Vehicle Setup] v7 轮子创建验证:\n" +
                  $"  WheelFL — pos={wheelFL.transform.localPosition:F6} rot(欧拉)={wheelFL.transform.localEulerAngles} rad={wheelFL.radius:F6}\n" +
                  $"  WheelFR — pos={wheelFR.transform.localPosition:F6} rot(欧拉)={wheelFR.transform.localEulerAngles} rad={wheelFR.radius:F6}\n" +
                  $"  WheelRL — pos={wheelRL.transform.localPosition:F6} rot(欧拉)={wheelRL.transform.localEulerAngles} rad={wheelRL.radius:F6}\n" +
                  $"  WheelRR — pos={wheelRR.transform.localPosition:F6} rot(欧拉)={wheelRR.transform.localEulerAngles} rad={wheelRR.radius:F6}");

        // ---- 6. VehicleController ----
        var controller = go.GetComponent<_Game.Systems.Vehicle.VehicleController>();
        if (controller == null) controller = go.AddComponent<_Game.Systems.Vehicle.VehicleController>();

        var vehicleData = AssetDatabase.LoadAssetAtPath<VehicleData>(
            "Assets/_Game/Config/VehicleData/OffRoad.asset");
        if (vehicleData != null)
        {
            // VehicleData 存储世界空间值（车长 4.5m 量级）
            vehicleData.colliderCenter = rawBounds.center * scaleFactor;
            vehicleData.colliderSize   = rawBounds.size * 0.95f * scaleFactor;

            // 驾驶员：车身中心偏前 + 偏左（左舵），世界空间
            Vector3 driverSeatWorld = rawBounds.center * scaleFactor
                + rawBounds.forwardAxis * (wsCarLength * 0.05f)
                - rawBounds.rightAxis   * (wsCarWidth  * 0.2f)
                + Vector3.up            * (wsCarHeight * 0.65f);
            vehicleData.driverSeatOffset = driverSeatWorld;

            // 下车点：车身左侧外，世界空间
            Vector3 exitWorld = rawBounds.center * scaleFactor
                - rawBounds.rightAxis * (wsCarWidth * 0.6f);
            vehicleData.exitOffset = exitWorld;

            EditorUtility.SetDirty(vehicleData);
            controller.vehicleData = vehicleData;
        }

        controller.wheelFL = wheelFL;
        controller.wheelFR = wheelFR;
        controller.wheelRL = wheelRL;
        controller.wheelRR = wheelRR;

        // ---- 7. VehicleInteraction ----
        var interaction = go.GetComponent<_Game.Systems.Vehicle.VehicleInteraction>();
        if (interaction == null) go.AddComponent<_Game.Systems.Vehicle.VehicleInteraction>();

        // ---- 8. Layer ----
        go.layer = LayerMask.NameToLayer("Default");

        // ---- 9. 保存 & 卸载 ----
        PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        PrefabUtility.UnloadPrefabContents(go);

        AssetDatabase.SaveAssets();

        Debug.Log($"[Vehicle Setup] v7 预制体已更新: {prefabPath}\n" +
                  $"  原始包围盒: {rawBounds.size.x:F4} × {rawBounds.size.y:F4} × {rawBounds.size.z:F4}\n" +
                  $"  根旋转={rootRot.eulerAngles}  世界车头={worldForward:F2}\n" +
                  $"  缩放因子: ×{scaleFactor:F1} → 世界车长 {wsCarLength:F1}m\n" +
                  $"  车轮世界位置: FL={worldFL:F2} FR={worldFR:F2} RL={worldRL:F2} RR={worldRR:F2}\n" +
                  $"  世界车轮半径={wsWheelRadius:F3}m  轮子已对齐。");
    }

    // ============================================================
    // 辅助：精确包围盒计算 v3
    // ============================================================

    /// <summary>
    /// 遍历所有 MeshFilter（含子节点），用 Mesh.bounds 的 8 个角点
    /// 从子节点本地空间变换到根节点本地空间，合并成精确 AABB。
    ///
    /// 相较于 renderer.bounds（世界空间 AABB），不会被子节点旋转膨大。
    /// </summary>
    private static BoundsInfo CalculateBounds(GameObject root)
    {
        var filters = root.GetComponentsInChildren<MeshFilter>();
        var skinnedRenderers = root.GetComponentsInChildren<SkinnedMeshRenderer>();

        Bounds? combined = null;

        // 处理普通 Mesh
        foreach (var mf in filters)
        {
            if (mf.sharedMesh == null) continue;

            // child 本地 → root 本地的变换矩阵
            Matrix4x4 childToRoot = root.transform.worldToLocalMatrix
                                  * mf.transform.localToWorldMatrix;

            Bounds localBounds = mf.sharedMesh.bounds;
            EncapsulateTransformedBounds(ref combined, localBounds, childToRoot);
        }

        // 处理 SkinnedMesh（取 bindpose 的 bounds）
        foreach (var sr in skinnedRenderers)
        {
            if (sr.sharedMesh == null) continue;

            Matrix4x4 childToRoot = root.transform.worldToLocalMatrix
                                  * sr.transform.localToWorldMatrix;

            Bounds localBounds = sr.sharedMesh.bounds;
            EncapsulateTransformedBounds(ref combined, localBounds, childToRoot);
        }

        if (!combined.HasValue)
        {
            combined = new Bounds(Vector3.zero, Vector3.one);
            Debug.LogWarning("[Vehicle Setup] 未找到任何 Mesh，使用默认包围盒。");
        }

        Bounds b = combined.Value;

        // ---- 自动检测车头朝向 ----
        // 规则：水平面上（XZ 平面）较长的轴 = 车头方向，较短的 = 车身侧面
        // Y = 始终为高度方向（Unity 世界 Y 朝上）
        float lenX = b.size.x;
        float lenZ = b.size.z;

        Vector3 fwdAxis, rightAxis;
        float carLength, carWidth;

        if (lenX >= lenZ)
        {
            // X 轴更长 → X = 车头方向
            fwdAxis   = b.center.x >= 0f ? Vector3.right : Vector3.left;
            rightAxis = Vector3.forward;  // Z = 车右
            carLength = lenX;
            carWidth  = lenZ;
        }
        else
        {
            // Z 轴更长 → Z = 车头方向
            fwdAxis   = b.center.z >= 0f ? Vector3.forward : Vector3.back;
            rightAxis = Vector3.right;    // X = 车右
            carLength = lenZ;
            carWidth  = lenX;
        }

        // 修正 rightAxis 符号：保证 rightAxis = cross(Vector3.up, forwardAxis)
        // （Unity 左手系：up×forward = right 的反方向？实际 Unity 是左手系，
        //  但 cross(up, forward) 给的是"看向 forward 时右手边"的 vector。
        //  我们直接用 cross 来保证正交且方向正确。）
        Vector3 computedRight = Vector3.Cross(Vector3.up, fwdAxis);
        if (Vector3.Dot(computedRight, rightAxis) < 0f)
            rightAxis = -rightAxis;

        // 底部 Y = 包围盒最低点（世界 Y 朝上）
        float bottomY = b.min.y;

        return new BoundsInfo
        {
            center      = b.center,
            size        = b.size,
            min         = b.min,
            max         = b.max,
            forwardAxis = fwdAxis,
            rightAxis   = rightAxis,
            upAxis      = Vector3.up,
            carLength   = carLength,
            carWidth    = carWidth,
            carHeight   = b.size.y,
            bottomY     = bottomY
        };
    }

    /// <summary>
    /// 将 mesh 的本地包围盒 8 个角点通过矩阵变换后，合并到 combined 中。
    /// </summary>
    private static void EncapsulateTransformedBounds(
        ref Bounds? combined, Bounds localBounds, Matrix4x4 transform)
    {
        Vector3 min = localBounds.min;
        Vector3 max = localBounds.max;

        // 8 个角点
        Vector3[] corners = new Vector3[8];
        corners[0] = new Vector3(min.x, min.y, min.z);
        corners[1] = new Vector3(min.x, min.y, max.z);
        corners[2] = new Vector3(min.x, max.y, min.z);
        corners[3] = new Vector3(min.x, max.y, max.z);
        corners[4] = new Vector3(max.x, min.y, min.z);
        corners[5] = new Vector3(max.x, min.y, max.z);
        corners[6] = new Vector3(max.x, max.y, min.z);
        corners[7] = new Vector3(max.x, max.y, max.z);

        for (int i = 0; i < 8; i++)
        {
            Vector3 p = transform.MultiplyPoint3x4(corners[i]);
            if (!combined.HasValue)
                combined = new Bounds(p, Vector3.zero);
            else
            {
                Bounds cb = combined.Value;
                cb.Encapsulate(p);
                combined = cb;
            }
        }
    }

    // ============================================================
    // 辅助：强制清除所有旧轮子子物体（v5 双重保险）
    // ============================================================

    private static void DestroyAllWheelChildren(GameObject root)
    {
        // 第一轮：按名称匹配删除
        string[] wheelNames = { "WheelFL", "WheelFR", "WheelRL", "WheelRR" };
        foreach (string name in wheelNames)
        {
            var t = root.transform.Find(name);
            if (t != null)
            {
                Object.DestroyImmediate(t.gameObject);
            }
        }

        // 第二轮：删除任何遗漏的 WheelCollider 子物体
        var remaining = root.GetComponentsInChildren<WheelCollider>();
        foreach (var wc in remaining)
        {
            if (wc == null || wc.gameObject == root) continue;
            Object.DestroyImmediate(wc.gameObject);
        }

        // 第三轮（保险）：直接遍历子节点找名称前缀匹配的
        for (int i = root.transform.childCount - 1; i >= 0; i--)
        {
            var child = root.transform.GetChild(i);
            if (child.name.StartsWith("Wheel"))
            {
                Object.DestroyImmediate(child.gameObject);
            }
        }

        Debug.Log("[Vehicle Setup] v7 已清除所有旧轮子子物体。");
    }

    // ============================================================
    // 辅助：创建 WheelCollider 子物体（v5: 不查找复用，始终新建）
    // ============================================================

    private static WheelCollider CreateWheelChild(GameObject parent, string name,
        Vector3 localPos, Quaternion localRot, float radius)
    {
        // v5: 始终创建全新 GameObject，不复用旧对象
        var wheelGo = new GameObject(name);
        wheelGo.transform.SetParent(parent.transform);
        wheelGo.transform.localPosition = localPos;
        wheelGo.transform.localRotation = localRot;
        wheelGo.transform.localScale    = Vector3.one;

        var wc = wheelGo.AddComponent<WheelCollider>();

        wc.mass = 20f;
        wc.radius = radius;
        wc.wheelDampingRate = 0.25f;
        wc.suspensionDistance = radius * 0.75f;

        var spring = wc.suspensionSpring;
        spring.spring = 35000f;
        spring.damper = 4500f;
        spring.targetPosition = 0.5f;
        wc.suspensionSpring = spring;

        var fwd = wc.forwardFriction;
        fwd.extremumSlip = 0.4f;
        fwd.extremumValue = 1f;
        fwd.asymptoteSlip = 0.8f;
        fwd.asymptoteValue = 0.5f;
        fwd.stiffness = 1.5f;
        wc.forwardFriction = fwd;

        var side = wc.sidewaysFriction;
        side.extremumSlip = 0.2f;
        side.extremumValue = 1f;
        side.asymptoteSlip = 0.5f;
        side.asymptoteValue = 0.75f;
        side.stiffness = 1.5f;
        wc.sidewaysFriction = side;

        return wc;
    }
}
