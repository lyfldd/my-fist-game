using System.Collections.Generic;
using UnityEngine;
using _Game.Core;
using _Game.Systems.WorldGen;
using _Game.Systems.Zombie;

namespace _Game.Systems.Audio
{
    /// <summary>
    /// 分贝系统 — 统一声音入口。节流 → Modifier管道 → 空间分区 → 通知。
    /// </summary>
    public class DecibelSystem : MonoBehaviour
    {
        public static DecibelSystem Instance { get; private set; }

        [Header("节流")]
        public float sourceMergeDistance = 3f;
        public int maxRecentEmissions = 16;

        // 每个 SoundTag 的冷却时间（秒）
        private static readonly Dictionary<SoundTag, float> Cooldowns = new()
        {
            { SoundTag.Footstep,   0.5f },
            { SoundTag.Combat,     0.3f },
            { SoundTag.Gunshot,    0.1f },
            { SoundTag.Building,   1.0f },
            { SoundTag.Mechanical, 2.0f },
            { SoundTag.Impact,     1.0f },
            { SoundTag.Voice,      2.0f },
        };

        // 持续声音配置
        private const float ContinuousEmitInterval = 1.5f;

        // 内部
        private readonly Dictionary<int, float> _sourceCooldowns = new();      // key → lastTime
        private readonly List<(Vector3 pos, float radius, float time)> _recentEmissions = new();
        private readonly Dictionary<string, ContinuousSound> _continuousSounds = new();

        private readonly List<ISoundModifier> _modifiers = new();
        private readonly List<ISoundListener> _listeners = new();
        private readonly List<ZombieStateMachine> _tempZombieList = new(64);

        private struct ContinuousSound
        {
            public Vector3 position;
            public float radius;
            public SoundSource source;
            public SoundTag tag;
            public float lastEmitTime;
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        void Update()
        {
            float now = UnityEngine.Time.time;

            // 持续声音自动 Emit（先收集再处理，避免枚举时修改字典）
            var toEmit = new List<(string key, ContinuousSound data)>();
            foreach (var kv in _continuousSounds)
            {
                var cs = kv.Value;
                if (now - cs.lastEmitTime >= ContinuousEmitInterval)
                {
                    cs.lastEmitTime = now;
                    toEmit.Add((kv.Key, cs));
                }
            }

            foreach (var (key, data) in toEmit)
            {
                _continuousSounds[key] = data;
                DoEmit(data.position, data.radius, data.source, data.tag, null);
            }
        }

        // ============================================================
        // 公开接口
        // ============================================================

        /// <summary>发出瞬时声音。</summary>
        public void Emit(Vector3 position, float radius, SoundSource source, SoundTag tag,
                         GameObject sourceObject = null)
        {
            DoEmit(position, radius, source, tag, sourceObject);
        }

        /// <summary>开始持续声音。已存在的 key 保留上次发射时间避免重复触发。</summary>
        public void StartContinuous(string key, Vector3 position, float radius,
                                   SoundSource source, SoundTag tag)
        {
            float prevTime = _continuousSounds.TryGetValue(key, out var existing)
                ? existing.lastEmitTime : 0f;

            _continuousSounds[key] = new ContinuousSound
            {
                position = position,
                radius = radius,
                source = source,
                tag = tag,
                lastEmitTime = prevTime
            };
        }

        /// <summary>更新持续声音的位置（移动声源）。</summary>
        public void UpdateContinuousPosition(string key, Vector3 newPosition)
        {
            if (_continuousSounds.TryGetValue(key, out var cs))
            {
                cs.position = newPosition;
                _continuousSounds[key] = cs;
            }
        }

        /// <summary>停止持续声音。</summary>
        public void StopContinuous(string key)
        {
            _continuousSounds.Remove(key);
        }

        public void RegisterModifier(ISoundModifier modifier)
        {
            if (!_modifiers.Contains(modifier))
                _modifiers.Add(modifier);
        }

        public void UnregisterModifier(ISoundModifier modifier)
        {
            _modifiers.Remove(modifier);
        }

        public void RegisterListener(ISoundListener listener)
        {
            if (!_listeners.Contains(listener))
                _listeners.Add(listener);
        }

        public void UnregisterListener(ISoundListener listener)
        {
            _listeners.Remove(listener);
        }

        // ============================================================
        // 内部处理
        // ============================================================

        void DoEmit(Vector3 position, float radius, SoundSource source, SoundTag tag,
                    GameObject sourceObject)
        {
            float now = UnityEngine.Time.time;

            // ── 节流 1: 同源冷却 ──
            int sourceId = sourceObject != null ? sourceObject.GetInstanceID() : 0;
            int key = sourceId ^ ((int)tag << 16);
            float cd = Cooldowns.TryGetValue(tag, out float c) ? c : 0.2f;

            if (_sourceCooldowns.TryGetValue(key, out float lastTime) && now - lastTime < cd)
                return;

            _sourceCooldowns[key] = now;
            CleanExpiredCooldowns(now);

            // ── 节流 2: 同位置合并 ──
            for (int i = _recentEmissions.Count - 1; i >= 0; i--)
            {
                var re = _recentEmissions[i];
                if (now - re.time > cd) continue;
                if (Vector3.Distance(position, re.pos) < sourceMergeDistance
                    && re.radius >= radius)
                    return; // 近处已有更大声音
            }

            _recentEmissions.Add((position, radius, now));
            if (_recentEmissions.Count > maxRecentEmissions)
                _recentEmissions.RemoveAt(0);

            // ── 构造事件 ──
            var noise = new NoiseEvent(position, radius, source, tag, sourceObject);

            // ── Modifier 管道 ──
            for (int i = 0; i < _modifiers.Count; i++)
                noise = _modifiers[i].Modify(noise);

            // ── 空间分区: 通知受影响 Chunk 内的僵尸 ──
            NotifyZombiesInRadius(noise);

            // ── 通用监听者 ──
            for (int i = _listeners.Count - 1; i >= 0; i--)
                _listeners[i]?.OnSoundHeard(noise);
        }

        // ============================================================
        // 空间分区
        // ============================================================

        void NotifyZombiesInRadius(NoiseEvent noise)
        {
            var cm = ChunkManager.Instance;
            if (cm == null) return;

            _tempZombieList.Clear();
            CollectZombiesInRadius(cm, noise.Position, noise.Radius, _tempZombieList);

            for (int i = 0; i < _tempZombieList.Count; i++)
            {
                var zombie = _tempZombieList[i];
                if (zombie == null) continue;

                float dist = Vector3.Distance(zombie.transform.position, noise.Position);
                if (dist <= noise.Radius)
                    zombie.OnSoundHeard(noise);
            }
        }

        void CollectZombiesInRadius(ChunkManager cm, Vector3 position, float radius,
                                    List<ZombieStateMachine> output)
        {
            int centerChunk = ChunkManager.GetChunkId(position);
            int chunkRadius = Mathf.CeilToInt(radius / GameConstants.RUNTIME_CHUNK_SIZE) + 1;
            int grid = GameConstants.CHUNK_GRID_SIZE;

            int cx = centerChunk % grid;
            int cz = centerChunk / grid;

            for (int dz = -chunkRadius; dz <= chunkRadius; dz++)
            {
                for (int dx = -chunkRadius; dx <= chunkRadius; dx++)
                {
                    int nx = cx + dx;
                    int nz = cz + dz;
                    if (nx < 0 || nx >= grid || nz < 0 || nz >= grid) continue;

                    int chunkId = nz * grid + nx;
                    var chunk = cm.GetChunk(chunkId);
                    if (chunk == null) continue;

                    output.AddRange(chunk.zombieInstances);
                }
            }
        }

        void CleanExpiredCooldowns(float now)
        {
            if (_sourceCooldowns.Count < 200) return;
            var expired = new List<int>();
            foreach (var kv in _sourceCooldowns)
                if (now - kv.Value > 5f) expired.Add(kv.Key);
            foreach (var k in expired)
                _sourceCooldowns.Remove(k);
        }

        // ============================================================
        // 公开属性（供 DebugPanel / DecibelHUD 读取）
        // ============================================================

        public int ModifierCount => _modifiers.Count;
        public int ListenerCount => _listeners.Count;
        public int ContinuousSoundCount => _continuousSounds.Count;
        public int SourceCooldownCount => _sourceCooldowns.Count;

        /// <summary>
        /// 查询某位置当前的感知噪声等级（0~1），基于最近 3s 内的声音发射。
        /// 等级 = (声音半径 / 最大半径) × (1 - 距离/半径)，体现不同声音类型的绝对响度差异。
        /// </summary>
        public float GetAmbientNoiseLevel(Vector3 position)
        {
            float maxLevel = 0f;
            float now = UnityEngine.Time.time;
            for (int i = _recentEmissions.Count - 1; i >= 0; i--)
            {
                var re = _recentEmissions[i];
                if (now - re.time > 3f) continue;
                float dist = Vector3.Distance(position, re.pos);
                if (dist < re.radius)
                {
                    float normalizedRadius = re.radius / GameConstants.MAX_SOUND_RADIUS;
                    float distanceAttenuation = 1f - (dist / re.radius);
                    float level = normalizedRadius * distanceAttenuation;
                    if (level > maxLevel) maxLevel = level;
                }
            }
            return maxLevel;
        }
    }
}
