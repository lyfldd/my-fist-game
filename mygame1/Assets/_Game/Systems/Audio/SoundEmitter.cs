using UnityEngine;
using _Game.Core;

namespace _Game.Systems.Audio
{
    /// <summary>
    /// 声源层 — 所有发出声音的系统调这里，不直接调 DecibelSystem。
    /// MonoBehaviour 单例：订阅 WeaponFiredEvent 自动处理枪声噪音。
    /// 静态方法保留供近战/建造/车辆/僵尸等系统直接调用。
    /// </summary>
    public class SoundEmitter : MonoBehaviour
    {
        public static SoundEmitter Instance { get; private set; }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        void OnEnable()
        {
            EventBus.Subscribe<WeaponFiredEvent>(OnWeaponFired);
            EventBus.Subscribe<WeaponDryFireEvent>(OnDryFire);
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<WeaponFiredEvent>(OnWeaponFired);
            EventBus.Unsubscribe<WeaponDryFireEvent>(OnDryFire);
        }

        // ============================================================
        // 事件驱动：武器开火 → 噪音（由 WeaponFiredEvent 携带 soundType）
        // ============================================================

        void OnWeaponFired(WeaponFiredEvent evt)
        {
            if (string.IsNullOrEmpty(evt.SoundType)) return;

            float radius = evt.SoundType switch
            {
                "Pistol" => 50f,
                "Rifle" => 80f,
                "Shotgun" => 70f,
                "Heavy" => 120f,
                _ => 50f
            };
            DecibelSystem.Instance?.Emit(evt.MuzzlePos, radius, SoundSource.Player, SoundTag.Gunshot);
            // TODO Phase4: AudioManager.PlayGunshot(evt.SoundType, evt.MuzzlePos);
        }

        void OnDryFire(WeaponDryFireEvent evt)
        {
            // 空仓咔咔声，极短距离噪音
            DecibelSystem.Instance?.Emit(evt.MuzzlePos, 3f, SoundSource.Player, SoundTag.Combat);
            // TODO Phase4: AudioManager.PlayDryFire(evt.MuzzlePos);
        }

        // ============================================================
        // 战斗（静态方法 — 供非武器系统调用）
        // ============================================================

        public static void EmitGunshot(Vector3 pos, bool isRifle = false)
        {
            float radius = isRifle ? 80f : 50f;
            DecibelSystem.Instance?.Emit(pos, radius, SoundSource.Player, SoundTag.Gunshot);
        }

        public static void EmitMeleeHit(Vector3 pos)
        {
            DecibelSystem.Instance?.Emit(pos, 15f, SoundSource.Player, SoundTag.Combat);
        }

        public static void EmitMeleeSwing(Vector3 pos)
        {
            DecibelSystem.Instance?.Emit(pos, 5f, SoundSource.Player, SoundTag.Combat);
        }

        // ============================================================
        // 移动（持续声音，由 PlayerController 管理 Start/Update/Stop）
        // ============================================================

        public static void SetFootstep(string key, Vector3 pos, bool running)
        {
            float radius = running ? 10f : 6f;
            DecibelSystem.Instance?.StartContinuous(key, pos, radius, SoundSource.Player, SoundTag.Footstep);
        }

        public static void StopFootstep(string key)
        {
            DecibelSystem.Instance?.StopContinuous(key);
        }

        // ============================================================
        // 建造
        // ============================================================

        public static void EmitBuildPlace(Vector3 pos)
        {
            DecibelSystem.Instance?.Emit(pos, 20f, SoundSource.Player, SoundTag.Building);
        }

        public static void EmitBuildDeconstruct(Vector3 pos)
        {
            DecibelSystem.Instance?.Emit(pos, 25f, SoundSource.Player, SoundTag.Building);
        }

        // ============================================================
        // 车辆
        // ============================================================

        public static void StartVehicleEngine(string key, Vector3 pos)
        {
            DecibelSystem.Instance?.StartContinuous(key, pos, 30f, SoundSource.Vehicle, SoundTag.Mechanical);
        }

        public static void UpdateVehicleEngine(string key, Vector3 pos)
        {
            DecibelSystem.Instance?.UpdateContinuousPosition(key, pos);
        }

        public static void StopVehicleEngine(string key)
        {
            DecibelSystem.Instance?.StopContinuous(key);
        }

        // ============================================================
        // 僵尸自身（Phase2 接入）
        // ============================================================

        public static void EmitZombieGroan(Vector3 pos, string key)
        {
            DecibelSystem.Instance?.StartContinuous(key, pos, 15f, SoundSource.Zombie, SoundTag.Voice);
        }

        public static void StopZombieGroan(string key)
        {
            DecibelSystem.Instance?.StopContinuous(key);
        }
    }
}
