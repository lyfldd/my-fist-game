using UnityEngine;
using _Game.Config;
using _Game.Core;
using _Game.Systems.Audio;

namespace _Game.Systems.Vehicle
{
    /// <summary>
    /// 车辆物理控制器 — 基于 Rigidbody + WheelCollider
    ///
    /// 职责：
    /// - 管理 4 个 WheelCollider 的 motorTorque / steerAngle / brakeTorque
    /// - 提供公共油门/转向/刹车接口（由 VehicleInputLock 调用）
    /// - 跟踪驾驶员引用和油量
    ///
    /// 挂载：车辆根 GameObject
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class VehicleController : MonoBehaviour
    {
        [Header("配置")]
        public VehicleData vehicleData;
        [Tooltip("燃料物品名（向后兼容，acceptedFuels 优先）")]
        public string fuelItemName = "SyntheticGasoline";

        [Header("车轮引用")]
        public WheelCollider wheelFL;
        public WheelCollider wheelFR;
        public WheelCollider wheelRL;
        public WheelCollider wheelRR;

        // 运行时状态
        public GameObject Driver { get; private set; }
        public bool HasDriver => Driver != null;
        public float CurrentFuel { get; private set; }
        public float CurrentSpeedKmh => _rb.velocity.magnitude * 3.6f;  // m/s → km/h
        public bool IsBoosting => _isBoosting;

        // 前置E：车辆血量
        public float CurrentHealth { get; private set; }
        public bool IsDestroyed => _isDestroyed;
        public float HealthPercent => vehicleData != null && vehicleData.maxHealth > 0f
            ? Mathf.Clamp01(CurrentHealth / vehicleData.maxHealth) : 1f;



        private Rigidbody _rb;
        private float _currentThrottle;   // -1(满倒车) ~ 0(空档) ~ 1(满油门)
        private float _currentSteer;      // -1(满左) ~ 1(满右)
        private float _currentBrake;      // 0 ~ 1
        private bool _isEngineOn;
        private bool _isBoosting;
        private bool _isDestroyed;         // 前置E
        private Inventory.Inventory _driverInventory;

        // 引擎声音 key（每辆车唯一）
        private string _engineSoundKey;
        private static bool _vehicleSoundDebugOnce;

        // 有效最高速度（根据是否加速动态计算）
        private float EffectiveMaxSpeed => _isBoosting
            ? _maxSpeed * (vehicleData != null ? vehicleData.boostSpeedMultiplier : 2f)
            : _maxSpeed;

        // 缓存的默认值（当 vehicleData 未设置时使用）
        private float _maxSpeed;
        private float _motorTorque;
        private float _brakingForce;
        private float _maxSteerAngle;
        private float _reverseTorque;

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _engineSoundKey = $"vehicle_engine_{GetInstanceID()}";
        }

        void OnDestroy()
        {
            SoundEmitter.StopVehicleEngine(_engineSoundKey);
        }

        void Start()
        {
            ApplyConfig();
        }

        void FixedUpdate()
        {
            if (_isDestroyed) return; // 前置E
            if (!HasDriver) return;
            if (!_isEngineOn) return;

            ApplyWheelPhysics();
            ClampSpeed();
            ApplyAntiRoll();
            ApplyRollStabilization();

            // 引擎声音跟随车辆位置
            if (_currentThrottle != 0)
                SoundEmitter.UpdateVehicleEngine(_engineSoundKey, transform.position);
        }

        /// <summary>
        /// 从 VehicleData 加载配置
        /// </summary>
        private void ApplyConfig()
        {
            if (vehicleData != null)
            {
                _maxSpeed = vehicleData.maxSpeed;
                _motorTorque = vehicleData.motorTorque;
                _brakingForce = vehicleData.brakingForce;
                _maxSteerAngle = vehicleData.maxSteerAngle;
                _reverseTorque = vehicleData.reverseTorque;
                _rb.mass = vehicleData.mass;
                CurrentFuel = vehicleData.fuelCapacity;
                if (CurrentHealth <= 0f) CurrentHealth = vehicleData.maxHealth;  // 前置E
            }
            else
            {
                _maxSpeed = 15f;
                _motorTorque = GameConstants.VEHICLE_DEFAULT_TORQUE;
                _brakingForce = GameConstants.VEHICLE_DEFAULT_BRAKE;
                _maxSteerAngle = GameConstants.VEHICLE_DEFAULT_STEER;
                _reverseTorque = GameConstants.VEHICLE_DEFAULT_REVERSE;
                _rb.mass = 1200f;
                CurrentFuel = GameConstants.VEHICLE_DEFAULT_FUEL;
            }

            // 一次性设置悬架阻尼（不在 FixedUpdate 里每帧覆盖）
            float springDamper = vehicleData != null && vehicleData.suspensionDamper >= 5000f
                ? vehicleData.suspensionDamper : 8000f;
            SetWheelSuspensionDamper(wheelFL, springDamper);
            SetWheelSuspensionDamper(wheelFR, springDamper);
            SetWheelSuspensionDamper(wheelRL, springDamper);
            SetWheelSuspensionDamper(wheelRR, springDamper);

            // 降低重心防侧翻
            Vector3 com = _rb.centerOfMass;
            float comYOffset = vehicleData != null ? vehicleData.centerOfMassYOffset : -0.3f;
            com.y += comYOffset;
            _rb.centerOfMass = com;
        }

        // ============================================================
        // 公共接口（由 VehicleInputLock 调用）
        // ============================================================

        /// <summary>
        /// 设置油门 [-1, 1]。正=前进, 负=倒车, 0=空档
        /// </summary>
        public void SetThrottle(float value)
        {
            _currentThrottle = Mathf.Clamp(value, -1f, 1f);
        }

        /// <summary>
        /// 设置转向 [-1, 1]。负=左转, 正=右转
        /// </summary>
        public void SetSteer(float value)
        {
            _currentSteer = Mathf.Clamp(value, -1f, 1f);
        }

        /// <summary>
        /// 设置刹车 [0, 1]
        /// </summary>
        public void SetBrake(float value)
        {
            _currentBrake = Mathf.Clamp01(value);
        }

        /// <summary>
        /// 设置加速模式（SHIFT 按下/松开）
        /// </summary>
        public void SetBoost(bool boosting)
        {
            _isBoosting = boosting;
        }

        /// <summary>
        /// 设置驾驶员（由 VehicleInteraction 调用）
        /// </summary>
        public void SetDriver(GameObject driver)
        {
            Driver = driver;
            bool wasEngineOn = _isEngineOn;
            _isEngineOn = Driver != null;
            _driverInventory = driver != null ? driver.GetComponent<Inventory.Inventory>() : null;

            if (_isEngineOn && !wasEngineOn)
            {
                SoundEmitter.StartVehicleEngine(_engineSoundKey, transform.position);
                if (!_vehicleSoundDebugOnce)
                {
                    _vehicleSoundDebugOnce = true;
                    Debug.Log($"[VehicleController] 引擎声音启动: key={_engineSoundKey}, pos={transform.position}");
                }
            }
            else if (!_isEngineOn)
            {
                _currentThrottle = 0;
                _currentSteer = 0;
                _currentBrake = 1f;
                SoundEmitter.StopVehicleEngine(_engineSoundKey);
            }
        }

        /// <summary>
        /// 立即停车（急刹）
        /// </summary>
        public void StopImmediate()
        {
            _currentThrottle = 0;
            _currentSteer = 0;
            _currentBrake = 1f;
            _rb.velocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }

        // ============================================================
        // 物理更新
        // ============================================================

        private void ApplyWheelPhysics()
        {
            float motorValue = 0f;
            float steerValue = _currentSteer * _maxSteerAngle;

            // 油门/倒车
            if (_currentThrottle > 0.01f)
            {
                motorValue = _currentThrottle * _motorTorque;
            }
            else if (_currentThrottle < -0.01f)
            {
                motorValue = _currentThrottle * _reverseTorque;
            }

            // 刹车优先于油门
            float brakeValue = _currentBrake * _brakingForce;
            if (_currentBrake > 0.01f)
            {
                motorValue = 0f;
            }

            // 前轮转向
            if (wheelFL != null)
            {
                wheelFL.steerAngle = steerValue;
                wheelFL.motorTorque = motorValue;
                wheelFL.brakeTorque = brakeValue;
            }
            if (wheelFR != null)
            {
                wheelFR.steerAngle = steerValue;
                wheelFR.motorTorque = motorValue;
                wheelFR.brakeTorque = brakeValue;
            }

            // 后轮只驱动（不转向）
            if (wheelRL != null)
            {
                wheelRL.motorTorque = motorValue;
                wheelRL.brakeTorque = brakeValue;
            }
            if (wheelRR != null)
            {
                wheelRR.motorTorque = motorValue;
                wheelRR.brakeTorque = brakeValue;
            }

            // 油耗（加速时 1.5 倍）
            if (_currentThrottle != 0 && vehicleData != null)
            {
                float fuelRate = vehicleData.fuelConsumptionRate;
                if (_isBoosting)
                    fuelRate *= vehicleData.boostFuelMultiplier;
                CurrentFuel -= fuelRate * UnityEngine.Time.fixedDeltaTime;
                if (CurrentFuel <= 0)
                {
                    CurrentFuel = 0;
                    // 尝试从驾驶员背包自动加油
                    if (!string.IsNullOrEmpty(fuelItemName) && _driverInventory != null)
                    {
                        int count = _driverInventory.CountItemByName(fuelItemName);
                        if (count > 0)
                        {
                            _driverInventory.RemoveItemByName(fuelItemName, 1);
                            CurrentFuel += 20f; // 1单位燃料≈20升
                            _isEngineOn = true;
                            return;
                        }
                    }
                    _isEngineOn = false;
                    SoundEmitter.StopVehicleEngine(_engineSoundKey);
                }
            }
        }

        private void ClampSpeed()
        {
            float effectiveMax = EffectiveMaxSpeed;
            if (_rb.velocity.magnitude > effectiveMax)
            {
                _rb.velocity = _rb.velocity.normalized * effectiveMax;
            }
        }

        /// <summary>
        /// 反侧倾杆 — 检测左右轮悬挂压缩差，施加反向力防止侧翻
        /// </summary>
        private void ApplyAntiRoll()
        {
            float stiffness = vehicleData != null ? vehicleData.antiRollStiffness : 5000f;
            ApplyAxleAntiRoll(wheelFL, wheelFR, stiffness);
            ApplyAxleAntiRoll(wheelRL, wheelRR, stiffness);
        }

        private void ApplyAxleAntiRoll(WheelCollider left, WheelCollider right, float stiffness)
        {
            if (left == null || right == null) return;

            WheelHit hit;
            // travel: 0 = 完全伸展, 1 = 完全压缩
            float travelL = 1f;
            float travelR = 1f;

            if (left.GetGroundHit(out hit))
                travelL = (-left.transform.InverseTransformPoint(hit.point).y - left.radius)
                        / left.suspensionDistance;
            if (right.GetGroundHit(out hit))
                travelR = (-right.transform.InverseTransformPoint(hit.point).y - right.radius)
                        / right.suspensionDistance;

            // 压缩差 → 反侧倾力：压缩多的一侧向上推，压缩少的一侧向下拉
            float antiRollForce = (travelL - travelR) * stiffness;

            if (left.isGrounded)
                _rb.AddForceAtPosition(left.transform.up * -antiRollForce, left.transform.position);
            if (right.isGrounded)
                _rb.AddForceAtPosition(right.transform.up * antiRollForce, right.transform.position);
        }

        /// <summary>
        /// 角速度阻尼 — 限制绕 X/Z 轴的旋转，防止车辆翻滚
        /// </summary>
        private void ApplyRollStabilization()
        {
            // 将世界空间角速度转到本地空间
            Vector3 localAngularVel = transform.InverseTransformDirection(_rb.angularVelocity);

            // 阻尼系数：值越大，翻滚越快被抑制
            const float pitchDamper = 0.95f;   // X 轴（前翻/后翻）
            const float rollDamper = 0.92f;    // Z 轴（侧翻）
            const float yawDamper = 1f;        // Y 轴（转向，不限制）

            localAngularVel.x *= pitchDamper;
            localAngularVel.z *= rollDamper;
            // Y 轴不处理，保持正常转向

            _rb.angularVelocity = transform.TransformDirection(localAngularVel);
        }

        private void SetWheelSuspensionDamper(WheelCollider wc, float damper)
        {
            if (wc == null) return;
            var suspension = wc.suspensionSpring;
            suspension.damper = damper;
            wc.suspensionSpring = suspension;
        }

        // ============================================================
        // 前置E：碰撞伤害
        // ============================================================

        void OnCollisionEnter(Collision col)
        {
            if (_isDestroyed || vehicleData == null || vehicleData.maxHealth <= 0f) return;

            float speed = col.relativeVelocity.magnitude;
            if (speed < GameConstants.VEHICLE_COLLISION_MIN_SPEED) return;

            float damage = speed * vehicleData.collisionDamageMult;
            CurrentHealth -= damage;

            if (CurrentHealth <= 0f)
            {
                CurrentHealth = 0f;
                DisableVehicle();
            }
        }

        void DisableVehicle()
        {
            if (_isDestroyed) return;
            _isDestroyed = true;
            _isEngineOn = false;

            // 弹飞驾驶员
            if (Driver != null)
            {
                var vi = Driver.GetComponent<VehicleInteraction>();
                if (vi != null) vi.Exit();
            }

            EventBus.Publish(new VehicleDestroyedEvent(gameObject,
                vehicleData != null ? vehicleData.vehicleName : "未知车辆"));
        }

    }
}
