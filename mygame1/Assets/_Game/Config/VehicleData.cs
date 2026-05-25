using UnityEngine;

namespace _Game.Config
{
    /// <summary>
    /// 车辆类别枚举
    /// </summary>
    public enum VehicleCategory
    {
        Car,        // 普通汽车（4座 + 后备箱）
        Truck,      // 卡车（2座 + 大货箱）
        Motorcycle, // 摩托车（1~2座，无后备箱）
        Bicycle     // 自行车（1座，纯人力）
    }

    /// <summary>
    /// 车辆配置数据 ScriptableObject
    /// 每种车型一个 .asset 文件
    /// </summary>
    [CreateAssetMenu(fileName = "NewVehicle", menuName = "Game/Vehicle Data")]
    public class VehicleData : ScriptableObject
    {
        [Header("基本信息")]
        public string vehicleName = "新车";
        public VehicleCategory category = VehicleCategory.Car;

        [Header("驾驶参数")]
        [Tooltip("最高速度 (m/s)")]
        public float maxSpeed = 15f;
        [Tooltip("加速度（电机扭矩），值越大加速越快")]
        public float motorTorque = 800f;
        [Tooltip("制动力")]
        public float brakingForce = 3000f;
        [Tooltip("最大转向角（度）")]
        public float maxSteerAngle = 35f;
        [Tooltip("倒车扭矩")]
        public float reverseTorque = 400f;

        [Header("物理参数")]
        [Tooltip("车身质量 (kg)")]
        public float mass = 1200f;
        [Tooltip("车轮悬架阻尼")]
        public float suspensionDamper = 8000f;

        [Header("加速/极速")]
        [Tooltip("SHIFT 加速时的最高速度倍率（不按 SHIFT = maxSpeed，按下 = maxSpeed * 此值）")]
        public float boostSpeedMultiplier = 2f;
        [Tooltip("SHIFT 加速时的油耗倍率")]
        public float boostFuelMultiplier = 1.5f;

        [Header("稳定性")]
        [Tooltip("反侧倾杆刚度 — 防止转弯时侧翻")]
        public float antiRollStiffness = 5000f;
        [Tooltip("重心 Y 偏移 — 负值降低重心增加稳定性")]
        public float centerOfMassYOffset = -0.3f;

        [Header("油量")]
        [Tooltip("油箱容量 (L)")]
        public float fuelCapacity = 40f;
        [Tooltip("每秒油耗 (L/s)")]
        public float fuelConsumptionRate = 0.02f;

        [Header("后备箱")]
        [Tooltip("后备箱格子列数")]
        public int trunkWidth = 4;
        [Tooltip("后备箱格子行数")]
        public int trunkHeight = 3;

        [Header("座位")]
        [Tooltip("驾驶员座位相对于车身的位置偏移")]
        public Vector3 driverSeatOffset = new Vector3(-0.5f, 0.5f, 0f);
        [Tooltip("下车时玩家出现的偏移（车身侧面）")]
        public Vector3 exitOffset = new Vector3(-2f, 0f, 0f);

        [Header("模型")]
        [Tooltip("车身碰撞体中心偏移")]
        public Vector3 colliderCenter = new Vector3(0f, 0.8f, 0f);
        [Tooltip("车身碰撞体尺寸")]
        public Vector3 colliderSize = new Vector3(2f, 1.5f, 4.5f);
    }
}
