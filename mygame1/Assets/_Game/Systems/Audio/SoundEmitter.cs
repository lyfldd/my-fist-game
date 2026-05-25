using UnityEngine;
using _Game.Core;

namespace _Game.Systems.Audio
{
    /// <summary>
    /// 声源层 — 所有发出声音的系统调这里，不直接调 DecibelSystem。
    /// 参数集中管理，未来加上 AudioManager 给玩家播放声音也在此接入。
    /// </summary>
    public static class SoundEmitter
    {
        // ============================================================
        // 战斗
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
            float radius = running ? 8f : 3f;
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
