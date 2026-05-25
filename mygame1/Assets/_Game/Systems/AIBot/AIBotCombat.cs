using UnityEngine;
using System.Collections.Generic;
using _Game.Systems.Combat;

namespace _Game.Systems.AIBot
{
    public enum RightArmWeapon { None, Pistol, Rifle, Shotgun, ElectromagneticRifle }
    public enum LeftArmWeapon { None, Shield, Chainsaw, Knife }
    public enum AttackPriority { Laser, RightArm, LeftArm }

    [System.Serializable]
    public class AmmoSlotData
    {
        public string ammoName;
        public int count;

        public bool IsEmpty => string.IsNullOrEmpty(ammoName) || count <= 0;
    }

    /// <summary>
    /// AI机器人战斗系统。内置激光 + 右臂远程武器 + 左臂近战/防御。
    /// 每 0.5s Tick 检测僵尸，各武器独立冷却。
    /// </summary>
    [RequireComponent(typeof(AIBot))]
    public class AIBotCombat : MonoBehaviour
    {
        [Header("警觉设置")]
        [Range(3f, 30f)]
        public float alertRange = 15f;
        public float alertRangeMin = 3f;
        public float alertRangeMax = 30f;

        [Header("攻击优先级")]
        public AttackPriority slot1 = AttackPriority.Laser;
        public AttackPriority slot2 = AttackPriority.RightArm;
        public AttackPriority slot3 = AttackPriority.LeftArm;

        [Header("武器挂载")]
        [SerializeField] private RightArmWeapon rightArm = RightArmWeapon.None;
        [SerializeField] private LeftArmWeapon leftArm = LeftArmWeapon.None;

        [Header("激光参数")]
        public float laserDamageBattery = 150f;
        public float laserDamageUranium = 225f;
        public float laserRangeBattery = 15f;
        public float laserRangeUranium = 22.5f;
        public float laserCooldown = 1f;

        [Header("右臂武器参数")]
        public float pistolDamage = 20f;
        public float pistolRange = 12f;
        public float pistolCooldown = 0.5f;
        public float rifleDamage = 30f;
        public float rifleRange = 18f;
        public float rifleCooldown = 0.75f;
        public float shotgunDamage = 25f;
        public float shotgunRange = 8f;
        public int shotgunTargets = 3;
        public float shotgunCooldown = 1f;
        public float emRifleDamage = 40f;
        public float emRifleRange = 20f;
        public float emRifleCooldown = 1.5f;

        [Header("左臂武器参数")]
        public float shieldReducePercent = 0.3f;
        public float chainsawDamagePerSec = 15f;
        public float chainsawRange = 3f;
        public float knifeDamage = 20f;
        public float knifeRange = 2f;
        public float knifeCooldown = 0.75f;

        [Header("检测层级")]
        public LayerMask zombieLayer = ~0;

        [Header("弹药存储 (4×4=16格, 每格上限999)")]
        [SerializeField] private List<AmmoSlotData> ammoSlots = new List<AmmoSlotData>();
        private const int AMMO_SLOT_COUNT = 16;
        private const int AMMO_MAX_PER_SLOT = 999;

        // 内部状态
        private AIBot _bot;
        private bool _nearPlayer;
        private float _laserTimer;
        private float _rightArmTimer;
        private float _leftArmTimer;
        private float _combatTickTimer;
        private const float COMBAT_TICK = 0.5f;

        // 碰撞检测缓存
        private readonly Collider[] _hitBuffer = new Collider[32];

        // 弹药物品名
        public const string AMMO_PISTOL = "手枪子弹";
        public const string AMMO_RIFLE = "步枪子弹";
        public const string AMMO_SHOTGUN = "霰弹";
        public const string AMMO_EM_RIFLE = "电池组";

        // 驾驶模式手动武器
        [HideInInspector] public AttackPriority manualWeaponSlot = AttackPriority.Laser;
        [HideInInspector] public bool aiWeaponOverride;

        // 委托：弹药消耗回调（由 AIBotInventory 在 Step 5 注册）
        public System.Func<string, int, bool> AmmoConsumeCallback;

        // 属性
        public RightArmWeapon CurrentRightArm => rightArm;
        public LeftArmWeapon CurrentLeftArm => leftArm;
        public AttackPriority[] PriorityOrder => new[] { slot1, slot2, slot3 };
        public bool HasShield => leftArm == LeftArmWeapon.Shield;
        public float DamageMultiplier => HasShield ? (1f - shieldReducePercent) : 1f;

        void Awake()
        {
            _bot = GetComponent<AIBot>();
            InitAmmoSlots();
        }

        void InitAmmoSlots()
        {
            if (ammoSlots == null || ammoSlots.Count == 0)
            {
                ammoSlots = new List<AmmoSlotData>(AMMO_SLOT_COUNT);
                for (int i = 0; i < AMMO_SLOT_COUNT; i++)
                    ammoSlots.Add(new AmmoSlotData());
            }
        }

        void Update()
        {
            // 判断是否在玩家身边
            _nearPlayer = _bot.CurrentCommand == AIBotCommand.Follow
                && _bot.PlayerTransform != null
                && Vector3.Distance(transform.position, _bot.PlayerTransform.position) <= _bot.followDistance + 2f;

            if (_bot.IsDead || _bot.IsShutdown) return;
            if (_bot.IsLowHP && !_nearPlayer) return;

            // 冷却计时（铀模式下快一倍）
            float dt = UnityEngine.Time.deltaTime * (_bot.CurrentEnergyMode == EnergyMode.Uranium ? 2f : 1f);
            if (_laserTimer > 0f) _laserTimer -= dt;
            if (_rightArmTimer > 0f) _rightArmTimer -= dt;
            if (_leftArmTimer > 0f) _leftArmTimer -= dt;

            // 左臂电锯持续伤害
            if (leftArm == LeftArmWeapon.Chainsaw && _leftArmTimer <= 0f)
            {
                TickChainsaw();
                _leftArmTimer = 1f; // 每秒Tick一次
            }

            // 定时检测僵尸
            _combatTickTimer -= UnityEngine.Time.deltaTime;
            if (_combatTickTimer <= 0f)
            {
                _combatTickTimer = COMBAT_TICK;
                TickCombat();
            }
        }

        // ============================================================
        // 战斗 Tick
        // ============================================================

        void TickCombat()
        {
            int hitCount = Physics.OverlapSphereNonAlloc(transform.position, alertRange, _hitBuffer, zombieLayer);
            if (hitCount == 0)
            {
                Debug.LogWarning($"[AIBotCombat] Tick: 未检测到任何Collider (range={alertRange}m, layerMask={zombieLayer.value})");
                return;
            }

            // 收集有效僵尸目标（有 DamageableZombie 且未死亡）
            int validCount = 0;
            for (int i = 0; i < hitCount; i++)
            {
                var dz = _hitBuffer[i].GetComponentInParent<DamageableZombie>();
                if (dz != null && !dz.IsDead)
                {
                    _hitBuffer[validCount] = _hitBuffer[i];
                    validCount++;
                }
            }

            if (validCount == 0)
            {
                // 诊断：检查第一个collider是什么
                var firstCol = _hitBuffer[0];
                var goName = firstCol != null ? firstCol.transform.parent?.name ?? firstCol.name : "null";
                Debug.LogWarning($"[AIBotCombat] Tick: 检测到 {hitCount} 个Collider，但无有效僵尸。首个:{goName}, layer:{firstCol?.gameObject.layer}");
                return;
            }

            // 在玩家身边时按距玩家距离排序（优先保护玩家），否则按距机器人距离
            Vector3 sortOrigin = _nearPlayer ? _bot.PlayerTransform.position : transform.position;
            System.Array.Sort(_hitBuffer, 0, validCount, new ZombieDistanceComparer(sortOrigin));

            // 按优先级给每个武器分配目标：高优先级武器获得更近的僵尸
            int laserTarget = -1, rightArmTarget = -1, leftArmTarget = -1;
            int nextTarget = 0;
            var priorityOrder = new[] { slot1, slot2, slot3 };

            for (int p = 0; p < 3; p++)
            {
                if (nextTarget >= validCount) break;
                switch (priorityOrder[p])
                {
                    case AttackPriority.Laser:   laserTarget = nextTarget; break;
                    case AttackPriority.RightArm: rightArmTarget = nextTarget; break;
                    case AttackPriority.LeftArm:  leftArmTarget = nextTarget; break;
                }
                nextTarget++;
            }

            // 没分配到目标的武器回退到最近的僵尸
            if (laserTarget < 0 && validCount > 0) laserTarget = 0;
            if (rightArmTarget < 0 && validCount > 0) rightArmTarget = 0;
            if (leftArmTarget < 0 && validCount > 0) leftArmTarget = 0;

            // 各武器独立开火（驾驶中跳过手动武器，除非AI接管）
            if (aiWeaponOverride || manualWeaponSlot != AttackPriority.Laser)
            {
                if (_laserTimer <= 0f && laserTarget >= 0 && CanFireLaser(laserTarget))
                    FireLaser(_hitBuffer[laserTarget]);
            }

            if (aiWeaponOverride || manualWeaponSlot != AttackPriority.RightArm)
            {
                if (_rightArmTimer <= 0f && rightArm != RightArmWeapon.None && rightArmTarget >= 0 && CanFireRightArm(rightArmTarget))
                    FireRightArm(_hitBuffer[rightArmTarget]);
            }

            if (aiWeaponOverride || manualWeaponSlot != AttackPriority.LeftArm)
            {
                if (_leftArmTimer <= 0f && leftArm != LeftArmWeapon.None && leftArm != LeftArmWeapon.Shield && leftArm != LeftArmWeapon.Chainsaw)
                {
                    if (leftArmTarget >= 0 && CanFireLeftArm(leftArmTarget))
                        FireLeftArm(_hitBuffer[leftArmTarget]);
                }
            }
        }

        // ============================================================
        // 内置激光
        // ============================================================

        bool CanFireLaser(int hitIndex)
        {
            if (hitIndex < 0) return false;
            var dz = _hitBuffer[hitIndex].GetComponentInParent<DamageableZombie>();
            if (dz == null || dz.IsDead) return false;

            float range = _bot.CurrentEnergyMode == EnergyMode.Uranium ? laserRangeUranium : laserRangeBattery;
            float dist = Vector3.Distance(transform.position, _hitBuffer[hitIndex].transform.position);
            return dist <= range;
        }

        void FireLaser(Collider targetCollider)
        {
            float damage = _bot.CurrentEnergyMode == EnergyMode.Uranium ? laserDamageUranium : laserDamageBattery;
            var dz = targetCollider.GetComponentInParent<DamageableZombie>();
            if (dz == null) return;

            dz.TakeDamage(damage);
            _laserTimer = laserCooldown;

            // 消耗能量
            _bot.ConsumeEnergyForAction(
                _bot.CurrentEnergyMode == EnergyMode.Uranium
                    ? AIBot.ENERGY_LASER_PER_SHOT * 0.5f
                    : AIBot.ENERGY_LASER_PER_SHOT);

            Debug.DrawLine(transform.position, targetCollider.transform.position, Color.red, 0.5f);
        }

        // ============================================================
        // 右臂远程武器
        // ============================================================

        bool CanFireRightArm(int hitIndex)
        {
            if (hitIndex < 0 || rightArm == RightArmWeapon.None) return false;
            var dz = _hitBuffer[hitIndex].GetComponentInParent<DamageableZombie>();
            if (dz == null || dz.IsDead) return false;

            float range = GetRightArmRange();
            float dist = Vector3.Distance(transform.position, _hitBuffer[hitIndex].transform.position);
            return dist <= range;
        }

        void FireRightArm(Collider targetCollider)
        {
            string ammoType = GetRightArmAmmo();
            if (!string.IsNullOrEmpty(ammoType))
            {
                if (!ConsumeAmmoInternal(ammoType, 1))
                    return; // 弹药不足
            }

            _bot.ConsumeEnergyForAction(AIBot.ENERGY_RIGHTARM_PER_SHOT);

            switch (rightArm)
            {
                case RightArmWeapon.Pistol:
                    FireSingleTarget(targetCollider, pistolDamage);
                    _rightArmTimer = pistolCooldown;
                    break;
                case RightArmWeapon.Rifle:
                    FireSingleTarget(targetCollider, rifleDamage);
                    _rightArmTimer = rifleCooldown;
                    break;
                case RightArmWeapon.Shotgun:
                    FireShotgun(targetCollider);
                    _rightArmTimer = shotgunCooldown;
                    break;
                case RightArmWeapon.ElectromagneticRifle:
                    FireEMRifle(targetCollider);
                    _rightArmTimer = emRifleCooldown;
                    break;
            }
        }

        void FireSingleTarget(Collider targetCollider, float damage)
        {
            var dz = targetCollider.GetComponentInParent<DamageableZombie>();
            if (dz != null) dz.TakeDamage(damage);
            Debug.DrawLine(transform.position, targetCollider.transform.position, Color.yellow, 0.3f);
        }

        void FireShotgun(Collider targetCollider)
        {
            // 锥形散射：打主目标 + 额外2个最近目标
            int hitCount = Physics.OverlapSphereNonAlloc(transform.position, GetRightArmRange(), _hitBuffer, zombieLayer);
            int hit = 0;
            for (int i = 0; i < hitCount && hit < shotgunTargets; i++)
            {
                var dz = _hitBuffer[i].GetComponentInParent<DamageableZombie>();
                if (dz == null || dz.IsDead) continue;

                // 锥形角度检查（前方90度）
                Vector3 toTarget = (_hitBuffer[i].transform.position - transform.position).normalized;
                if (Vector3.Angle(transform.forward, toTarget) < 45f)
                {
                    dz.TakeDamage(shotgunDamage);
                    Debug.DrawLine(transform.position, _hitBuffer[i].transform.position, Color.yellow, 0.3f);
                    hit++;
                }
            }
        }

        void FireEMRifle(Collider targetCollider)
        {
            // 穿透一排：打主目标 + 其背后的僵尸
            Vector3 dir = (targetCollider.transform.position - transform.position).normalized;
            float range = GetRightArmRange();

            var targets = Physics.RaycastAll(transform.position, dir, range, zombieLayer);
            foreach (var hit in targets)
            {
                var dz = hit.collider.GetComponentInParent<DamageableZombie>();
                if (dz != null && !dz.IsDead)
                {
                    dz.TakeDamage(emRifleDamage);
                    Debug.DrawLine(transform.position, hit.point, Color.cyan, 0.5f);
                }
            }
        }

        float GetRightArmRange()
        {
            switch (rightArm)
            {
                case RightArmWeapon.Pistol: return pistolRange;
                case RightArmWeapon.Rifle: return rifleRange;
                case RightArmWeapon.Shotgun: return shotgunRange;
                case RightArmWeapon.ElectromagneticRifle: return emRifleRange;
                default: return 0f;
            }
        }

        string GetRightArmAmmo()
        {
            switch (rightArm)
            {
                case RightArmWeapon.Pistol: return AMMO_PISTOL;
                case RightArmWeapon.Rifle: return AMMO_RIFLE;
                case RightArmWeapon.Shotgun: return AMMO_SHOTGUN;
                case RightArmWeapon.ElectromagneticRifle: return AMMO_EM_RIFLE;
                default: return null;
            }
        }

        // ============================================================
        // 左臂近战
        // ============================================================

        bool CanFireLeftArm(int hitIndex)
        {
            if (hitIndex < 0 || leftArm == LeftArmWeapon.None || leftArm == LeftArmWeapon.Shield)
                return false;
            var dz = _hitBuffer[hitIndex].GetComponentInParent<DamageableZombie>();
            if (dz == null || dz.IsDead) return false;

            float range = leftArm == LeftArmWeapon.Knife ? knifeRange : chainsawRange;
            float dist = Vector3.Distance(transform.position, _hitBuffer[hitIndex].transform.position);
            return dist <= range;
        }

        void FireLeftArm(Collider targetCollider)
        {
            _bot.ConsumeEnergyForAction(AIBot.ENERGY_LEFTARM_PER_SWING);

            if (leftArm == LeftArmWeapon.Knife)
            {
                var dz = targetCollider.GetComponentInParent<DamageableZombie>();
                if (dz != null) dz.TakeDamage(knifeDamage);
                _leftArmTimer = knifeCooldown;
                Debug.DrawLine(transform.position, targetCollider.transform.position, Color.white, 0.2f);
            }
        }

        void TickChainsaw()
        {
            int hitCount = Physics.OverlapSphereNonAlloc(transform.position, chainsawRange, _hitBuffer, zombieLayer);
            for (int i = 0; i < hitCount; i++)
            {
                var dz = _hitBuffer[i].GetComponentInParent<DamageableZombie>();
                if (dz != null && !dz.IsDead)
                {
                    dz.TakeDamage(chainsawDamagePerSec);
                    _bot.ConsumeEnergyForAction(AIBot.ENERGY_LEFTARM_PER_SWING);
                }
            }
        }

        // ============================================================
        // 武器装卸
        // ============================================================

        public void EquipRightArm(RightArmWeapon weapon) => rightArm = weapon;
        public void UnequipRightArm() => rightArm = RightArmWeapon.None;
        public void EquipLeftArm(LeftArmWeapon weapon) => leftArm = weapon;
        public void UnequipLeftArm() => leftArm = LeftArmWeapon.None;

        // ============================================================
        // 弹药存储 (4×4=16格)
        // ============================================================

        /// <summary>获取某种弹药的总数</summary>
        public int GetAmmoCount(string ammoName)
        {
            int total = 0;
            foreach (var slot in ammoSlots)
            {
                if (slot.ammoName == ammoName)
                    total += slot.count;
            }
            return total;
        }

        /// <summary>装入弹药，返回实际装入数量</summary>
        public int LoadAmmo(string ammoName, int count)
        {
            int remaining = count;
            // 优先堆叠到已有同类槽
            foreach (var slot in ammoSlots)
            {
                if (slot.ammoName == ammoName && slot.count < AMMO_MAX_PER_SLOT)
                {
                    int canAdd = Mathf.Min(remaining, AMMO_MAX_PER_SLOT - slot.count);
                    slot.count += canAdd;
                    remaining -= canAdd;
                    if (remaining <= 0) break;
                }
            }
            // 填入空槽
            if (remaining > 0)
            {
                foreach (var slot in ammoSlots)
                {
                    if (slot.IsEmpty)
                    {
                        slot.ammoName = ammoName;
                        int canAdd = Mathf.Min(remaining, AMMO_MAX_PER_SLOT);
                        slot.count = canAdd;
                        remaining -= canAdd;
                        if (remaining <= 0) break;
                    }
                }
            }
            return count - remaining;
        }

        /// <summary>取出弹药，返回实际取出数量</summary>
        public int UnloadAmmo(string ammoName, int count)
        {
            int remaining = count;
            foreach (var slot in ammoSlots)
            {
                if (slot.ammoName == ammoName && slot.count > 0)
                {
                    int toRemove = Mathf.Min(slot.count, remaining);
                    slot.count -= toRemove;
                    remaining -= toRemove;
                    if (slot.count <= 0)
                        slot.ammoName = null;
                    if (remaining <= 0) break;
                }
            }
            return count - remaining;
        }

        /// <summary>消耗弹药（战斗用）</summary>
        bool ConsumeAmmoInternal(string ammoName, int count)
        {
            return UnloadAmmo(ammoName, count) >= count;
        }

        /// <summary>弹药槽是否已满</summary>
        public bool IsAmmoFull => ammoSlots.TrueForAll(s => !s.IsEmpty);

        /// <summary>获取所有弹药槽快照</summary>
        public List<AmmoSlotData> GetAmmoSlots() => new List<AmmoSlotData>(ammoSlots);

        // ============================================================
        // 武器 ↔ 物品名映射
        // ============================================================

        public static string GetAmmoNameForWeapon(RightArmWeapon weapon)
        {
            switch (weapon)
            {
                case RightArmWeapon.Pistol: return AMMO_PISTOL;
                case RightArmWeapon.Rifle: return AMMO_RIFLE;
                case RightArmWeapon.Shotgun: return AMMO_SHOTGUN;
                case RightArmWeapon.ElectromagneticRifle: return AMMO_EM_RIFLE;
                default: return null;
            }
        }

        public static string GetWeaponItemName(RightArmWeapon weapon)
        {
            switch (weapon)
            {
                case RightArmWeapon.Pistol: return "手枪";
                case RightArmWeapon.Rifle: return "步枪";
                case RightArmWeapon.Shotgun: return "霰弹枪";
                case RightArmWeapon.ElectromagneticRifle: return "电磁步枪";
                default: return null;
            }
        }

        public static string GetWeaponItemName(LeftArmWeapon weapon)
        {
            switch (weapon)
            {
                case LeftArmWeapon.Shield: return "盾牌";
                case LeftArmWeapon.Chainsaw: return "电锯";
                case LeftArmWeapon.Knife: return "短刀";
                default: return null;
            }
        }

        public static string GetAmmoDisplayName(string ammoName)
        {
            switch (ammoName)
            {
                case AMMO_PISTOL: return "手枪子弹";
                case AMMO_RIFLE: return "步枪子弹";
                case AMMO_SHOTGUN: return "霰弹";
                case AMMO_EM_RIFLE: return "电池组";
                default: return ammoName;
            }
        }

        public static RightArmWeapon GetRightArmFromItemName(string itemName)
        {
            switch (itemName)
            {
                case "手枪": return RightArmWeapon.Pistol;
                case "步枪": return RightArmWeapon.Rifle;
                case "霰弹枪": return RightArmWeapon.Shotgun;
                case "电磁步枪": return RightArmWeapon.ElectromagneticRifle;
                default: return RightArmWeapon.None;
            }
        }

        public static LeftArmWeapon GetLeftArmFromItemName(string itemName)
        {
            switch (itemName)
            {
                case "盾牌": return LeftArmWeapon.Shield;
                case "电锯": return LeftArmWeapon.Chainsaw;
                case "短刀": return LeftArmWeapon.Knife;
                default: return LeftArmWeapon.None;
            }
        }

        // ============================================================
        // 优先级切换
        // ============================================================

        public void CyclePriority()
        {
            var temp = slot1;
            slot1 = slot2;
            slot2 = slot3;
            slot3 = temp;
        }

        // ============================================================
        // 手动开火（驾驶模式）
        // ============================================================

        /// <summary>激光自动锁敌：扫描范围内最近僵尸→锁定→发射。</summary>
        public void ManualFireLaser()
        {
            if (_laserTimer > 0f) { Debug.Log("[AIBotCombat] 激光冷却中"); return; }

            float range = _bot.CurrentEnergyMode == EnergyMode.Uranium ? laserRangeUranium : laserRangeBattery;
            int hitCount = Physics.OverlapSphereNonAlloc(transform.position, range, _hitBuffer, zombieLayer);
            if (hitCount == 0) { Debug.Log("[AIBotCombat] 激光范围内无目标"); return; }

            // 找最近的活僵尸
            DamageableZombie nearest = null;
            float nearestDist = float.MaxValue;
            for (int i = 0; i < hitCount; i++)
            {
                var dz = _hitBuffer[i].GetComponentInParent<DamageableZombie>();
                if (dz == null || dz.IsDead) continue;
                float d = Vector3.Distance(transform.position, _hitBuffer[i].transform.position);
                if (d < nearestDist) { nearestDist = d; nearest = dz; }
            }

            if (nearest == null) { Debug.Log("[AIBotCombat] 范围内无活僵尸"); return; }

            float damage = _bot.CurrentEnergyMode == EnergyMode.Uranium ? laserDamageUranium : laserDamageBattery;
            nearest.TakeDamage(damage);
            _laserTimer = laserCooldown;
            Debug.Log($"[AIBotCombat] 激光命中 {nearest.name}, 伤害={damage}");

            _bot.ConsumeEnergyForAction(
                _bot.CurrentEnergyMode == EnergyMode.Uranium
                    ? AIBot.ENERGY_LASER_PER_SHOT * 0.5f
                    : AIBot.ENERGY_LASER_PER_SHOT);

            Debug.DrawLine(transform.position, nearest.transform.position, Color.red, 0.5f);
        }

        /// <summary>手动瞄准开火（右臂/左臂）：Raycast从机器人朝向aimPosition。</summary>
        public void ManualFireAimed(Vector3 aimPosition)
        {
            Vector3 dir = (aimPosition - transform.position).normalized;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) dir = transform.forward;

            if (manualWeaponSlot == AttackPriority.RightArm)
            {
                if (_rightArmTimer > 0f) { Debug.Log("[AIBotCombat] 右臂冷却中"); return; }
                if (rightArm == RightArmWeapon.None) { Debug.Log("[AIBotCombat] 右臂未装备"); return; }

                string ammoType = GetRightArmAmmo();
                if (!string.IsNullOrEmpty(ammoType) && !ConsumeAmmoInternal(ammoType, 1))
                { Debug.Log($"[AIBotCombat] 弹药不足: {ammoType}"); return; }

                _bot.ConsumeEnergyForAction(AIBot.ENERGY_RIGHTARM_PER_SHOT);

                float range = GetRightArmRange();
                Vector3 origin = transform.position + Vector3.up * 0.5f;
                Debug.DrawRay(origin, dir * range, Color.red, 1f);
                if (Physics.Raycast(origin, dir, out RaycastHit hit, range, zombieLayer))
                {
                    var dz = hit.collider.GetComponentInParent<DamageableZombie>();
                    if (dz != null && !dz.IsDead)
                    {
                        float dmg = GetRightArmDamage();
                        dz.TakeDamage(dmg);
                        Debug.Log($"[AIBotCombat] 右臂命中 {dz.name}, 伤害={dmg}");
                        Debug.DrawLine(origin, hit.point, Color.yellow, 0.3f);
                    }
                }
                else
                {
                    Debug.Log("[AIBotCombat] 右臂射线未命中");
                    Debug.DrawRay(origin, dir * range, Color.white, 0.1f);
                }

                _rightArmTimer = GetRightArmCooldown();
            }
            else if (manualWeaponSlot == AttackPriority.LeftArm)
            {
                if (_leftArmTimer > 0f) { Debug.Log("[AIBotCombat] 左臂冷却中"); return; }
                if (leftArm == LeftArmWeapon.None) { Debug.Log("[AIBotCombat] 左臂未装备"); return; }
                if (leftArm == LeftArmWeapon.Shield) { Debug.Log("[AIBotCombat] 盾牌不能攻击"); return; }

                _bot.ConsumeEnergyForAction(AIBot.ENERGY_LEFTARM_PER_SWING);

                float range = leftArm == LeftArmWeapon.Knife ? knifeRange : chainsawRange;
                Vector3 origin = transform.position + Vector3.up * 0.5f;
                Debug.DrawRay(origin, dir * range, Color.red, 1f);
                if (Physics.Raycast(origin, dir, out RaycastHit hit, range, zombieLayer))
                {
                    var dz = hit.collider.GetComponentInParent<DamageableZombie>();
                    if (dz != null && !dz.IsDead)
                    {
                        float dmg = leftArm == LeftArmWeapon.Knife ? knifeDamage : chainsawDamagePerSec;
                        dz.TakeDamage(dmg);
                        Debug.Log($"[AIBotCombat] 左臂命中 {dz.name}, 伤害={dmg}");
                        Debug.DrawLine(origin, hit.point, Color.white, 0.2f);
                    }
                }
                else
                {
                    Debug.Log("[AIBotCombat] 左臂射线未命中");
                }

                _leftArmTimer = leftArm == LeftArmWeapon.Knife ? knifeCooldown : 1f;
            }
        }

        float GetRightArmDamage()
        {
            return rightArm switch
            {
                RightArmWeapon.Pistol => pistolDamage,
                RightArmWeapon.Rifle => rifleDamage,
                RightArmWeapon.Shotgun => shotgunDamage,
                RightArmWeapon.ElectromagneticRifle => emRifleDamage,
                _ => 0f
            };
        }

        float GetRightArmCooldown()
        {
            return rightArm switch
            {
                RightArmWeapon.Pistol => pistolCooldown,
                RightArmWeapon.Rifle => rifleCooldown,
                RightArmWeapon.Shotgun => shotgunCooldown,
                RightArmWeapon.ElectromagneticRifle => emRifleCooldown,
                _ => 1f
            };
        }

        // ============================================================
        // 距离排序比较器
        // ============================================================

        class ZombieDistanceComparer : System.Collections.Generic.IComparer<Collider>
        {
            private Vector3 _origin;
            public ZombieDistanceComparer(Vector3 origin) => _origin = origin;

            public int Compare(Collider a, Collider b)
            {
                float da = Vector3.Distance(_origin, a.transform.position);
                float db = Vector3.Distance(_origin, b.transform.position);
                return da.CompareTo(db);
            }
        }
    }
}
