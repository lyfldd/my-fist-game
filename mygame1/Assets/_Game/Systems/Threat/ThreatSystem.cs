using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using _Game.Config;
using _Game.Core;
using UnityTime = UnityEngine.Time;

namespace _Game.Systems.Threat
{
    /// <summary>
    /// 威胁系统 — 动态仇恨表，中心单例。
    /// 任何实体通过 AddThreat 报告威胁，通过 GetTopTarget 查询该打谁。
    /// </summary>
    public class ThreatSystem : MonoBehaviour
    {
        public static ThreatSystem Instance { get; private set; }

        [Header("阈值")]
        [SerializeField] float _maxThreatPerTarget = 500f;
        [SerializeField] int _maxTrackedTargets = 5;
        [SerializeField] float _friendlyFireMultiplier = 0.1f;
        [SerializeField] float _perceptionRange = 30f;

        // 核心表：外层key = sourceId（谁存有仇恨），内层key = targetId 或 0(sentinel)
        Dictionary<int, Dictionary<int, ThreatEntry>> _table = new();

        // 实体 → 阵营
        Dictionary<int, FactionType> _entityFactions = new();

        // Neutral 临时敌对标记
        Dictionary<(int, int), float> _tempHostileTimers = new();

        // 分帧 GC
        Queue<int> _gcQueue;
        const int GC_BATCH = 5;

        // 帧标记（供 AIAgent 运行时断言）
        int _frameProcessed;
        bool _eventSubscribed;

        // ═══ 生命周期 ═══
        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            SubscribeEvents();
        }

        void OnDestroy()
        {
            UnsubscribeEvents();
            if (Instance == this) Instance = null;
        }

        void SubscribeEvents()
        {
            if (_eventSubscribed) return;
            EventBus.Subscribe<ThreatReportEvent>(OnThreatReport);
            EventBus.Subscribe<EntityDeathEvent>(OnEntityDeath);
            EventBus.Subscribe<FactionRelationChangedEvent>(OnRelationChanged);
            _eventSubscribed = true;
        }

        void UnsubscribeEvents()
        {
            if (!_eventSubscribed) return;
            EventBus.Unsubscribe<ThreatReportEvent>(OnThreatReport);
            EventBus.Unsubscribe<EntityDeathEvent>(OnEntityDeath);
            EventBus.Unsubscribe<FactionRelationChangedEvent>(OnRelationChanged);
            _eventSubscribed = false;
        }

        void Update()
        {
            TickTempHostile(UnityTime.deltaTime);
            DecayAll(UnityTime.deltaTime);
            RunFrameGC();
            _frameProcessed = UnityTime.frameCount;
        }

        // ═══ 注册/注销 ═══
        public void RegisterEntity(int instanceId, FactionType faction)
        {
            _entityFactions[instanceId] = faction;
        }

        public void UnregisterEntity(int instanceId)
        {
            _table.Remove(instanceId);
            foreach (var inner in _table.Values)
                inner.Remove(instanceId);
            _entityFactions.Remove(instanceId);

            // 清理临时敌对中涉及该实体的条目
            var toRemove = _tempHostileTimers.Keys
                .Where(k => k.Item1 == instanceId || k.Item2 == instanceId)
                .ToList();
            foreach (var k in toRemove)
                _tempHostileTimers.Remove(k);
        }

        public bool IsEntityRegistered(int instanceId)
            => _entityFactions.ContainsKey(instanceId);

        public FactionType GetFaction(int instanceId)
            => _entityFactions.TryGetValue(instanceId, out var f) ? f : FactionType.Neutral;

        // ═══ 帧处理标记 ═══
        public bool HasProcessedThisFrame() => _frameProcessed == UnityTime.frameCount;

        // ═══ 核心：添加威胁 ═══
        public void AddThreat(int sourceId, int? targetId, float value, ThreatType type, Vector3? position = null)
        {
            int internalKey = targetId ?? 0;
            if (sourceId == internalKey && internalKey != 0) return;

            var srcFac = GetFaction(sourceId);

            // 阵营系数
            float multiplier = 1f;
            if (targetId.HasValue)
            {
                var tgtFac = GetFaction(targetId.Value);

                // Neutral 双向临时敌对
                if (tgtFac == FactionType.Neutral && srcFac != FactionType.Neutral)
                {
                    _tempHostileTimers[(targetId.Value, sourceId)] = 30f;
                    _tempHostileTimers[(sourceId, targetId.Value)] = 30f;
                }
                else if (srcFac == FactionType.Neutral && tgtFac != FactionType.Neutral)
                {
                    _tempHostileTimers[(sourceId, targetId.Value)] = 30f;
                    _tempHostileTimers[(targetId.Value, sourceId)] = 30f;
                }

                if (FactionSystem.Instance != null &&
                    FactionSystem.Instance.IsAlly(srcFac, tgtFac) &&
                    !IsTempHostile(targetId.Value, sourceId))
                    multiplier = _friendlyFireMultiplier;
            }

            value *= multiplier;
            if (value <= 0f) return;

            // 写入表
            if (!_table.TryGetValue(sourceId, out var inner))
            {
                inner = new Dictionary<int, ThreatEntry>();
                _table[sourceId] = inner;
            }

            if (inner.TryGetValue(internalKey, out var existing))
            {
                existing.value += value;
                existing.value = Mathf.Min(existing.value, _maxThreatPerTarget);
                existing.timestamp = UnityTime.time;
                existing.lastKnownPos = position ?? existing.lastKnownPos;
                inner[internalKey] = existing;
            }
            else
            {
                if (inner.Count >= _maxTrackedTargets)
                {
                    int weakestId = inner.OrderBy(kv => kv.Value.WeightedValue).First().Key;
                    inner.Remove(weakestId);
                }

                inner[internalKey] = new ThreatEntry
                {
                    sourceInstanceId = sourceId,
                    targetInstanceId = targetId,
                    value = Mathf.Min(value, _maxThreatPerTarget),
                    timestamp = UnityTime.time,
                    type = type,
                    lastKnownPos = position ?? Vector3.zero,
                    sourceFactionType = targetId.HasValue ? GetFaction(targetId.Value) : null
                };
            }
        }

        // ═══ 查询 ═══
        public int? GetTopTarget(int sourceId)
        {
            if (!_table.TryGetValue(sourceId, out var inner) || inner.Count == 0)
                return null;

            int? best = null;
            float bestWeighted = float.MinValue;

            foreach (var (tgtId, entry) in inner)
            {
                float w = entry.WeightedValue;
                if (w > bestWeighted)
                {
                    bestWeighted = w;
                    best = tgtId == 0 ? null : tgtId;
                }
            }

            return best;
        }

        public int? GetTopRealTarget(int sourceId)
        {
            if (!_table.TryGetValue(sourceId, out var inner) || inner.Count == 0)
                return null;

            int? best = null;
            float bestWeighted = float.MinValue;

            foreach (var (tgtId, entry) in inner)
            {
                if (tgtId == 0 || !entry.targetInstanceId.HasValue) continue;
                float w = entry.WeightedValue;
                if (w > bestWeighted)
                {
                    bestWeighted = w;
                    best = tgtId;
                }
            }

            return best;
        }

        public List<ThreatEntry> GetAllThreats(int sourceId)
        {
            if (!_table.TryGetValue(sourceId, out var inner))
                return new List<ThreatEntry>();
            return inner.Values.OrderByDescending(e => e.WeightedValue).ToList();
        }

        public float GetThreatValue(int sourceId, int targetId)
        {
            if (!_table.TryGetValue(sourceId, out var inner)) return 0f;
            return inner.TryGetValue(targetId, out var e) ? e.value : 0f;
        }

        public float GetWeightedValue(int sourceId, int targetId)
        {
            if (!_table.TryGetValue(sourceId, out var inner)) return 0f;
            return inner.TryGetValue(targetId, out var e) ? e.WeightedValue : 0f;
        }

        public bool HasThreat(int sourceId)
            => _table.TryGetValue(sourceId, out var inner) && inner.Count > 0;

        public Vector3? GetLastKnownPosition(int sourceId, int targetId)
        {
            if (!_table.TryGetValue(sourceId, out var inner)) return null;
            return inner.TryGetValue(targetId, out var e) ? e.lastKnownPos : null;
        }

        // ═══ 清除 ═══
        public void ClearThreat(int sourceId, int targetId)
        {
            if (_table.TryGetValue(sourceId, out var inner))
                inner.Remove(targetId);
        }

        public void ClearAllThreats(int sourceId)
        {
            _table.Remove(sourceId);
        }

        // ═══ 衰减 ═══
        void DecayAll(float deltaTime)
        {
            var outerToRemove = new List<int>();

            foreach (var (srcId, inner) in _table)
            {
                var srcPos = InstanceRegistry.GetTransform(srcId)?.position ?? Vector3.zero;
                bool srcExists = InstanceRegistry.Exists(srcId);
                var innerToRemove = new List<int>();

                foreach (var tgtId in inner.Keys.ToList())
                {
                    var e = inner[tgtId];
                    float decayRate = ThreatTypeConfig.GetDecayRate(e.type);

                    // 距离衰减加速
                    if (srcExists && e.lastKnownPos != Vector3.zero)
                    {
                        float dist = Vector3.Distance(srcPos, e.lastKnownPos);
                        if (dist > _perceptionRange * 2f)
                            decayRate *= 3f;
                        else if (dist > _perceptionRange)
                            decayRate *= 1.5f;
                    }

                    e.value -= decayRate * deltaTime;
                    if (e.value <= 0f)
                        innerToRemove.Add(tgtId);
                    else
                        inner[tgtId] = e;
                }

                foreach (var id in innerToRemove)
                    inner.Remove(id);

                if (inner.Count == 0)
                    outerToRemove.Add(srcId);
            }

            foreach (var id in outerToRemove)
                _table.Remove(id);
        }

        // ═══ Neutral 临时敌对 ═══
        bool IsTempHostile(int entityA, int entityB)
            => _tempHostileTimers.TryGetValue((entityA, entityB), out var t) && t > 0f
            || _tempHostileTimers.TryGetValue((entityB, entityA), out t) && t > 0f;

        void TickTempHostile(float dt)
        {
            var expired = new List<(int, int)>();
            // 快照迭代，避免修改集合
            foreach (var key in _tempHostileTimers.Keys.ToList())
            {
                float remaining = _tempHostileTimers[key];
                float newRemaining = remaining - dt;
                if (newRemaining <= 0f)
                    expired.Add(key);
                else
                    _tempHostileTimers[key] = newRemaining;
            }
            foreach (var k in expired)
                _tempHostileTimers.Remove(k);
        }

        // ═══ 事件处理 ═══
        void OnThreatReport(ThreatReportEvent e)
        {
            AddThreat(e.TargetInstanceId, e.SourceInstanceId, e.DamageAmount, ThreatType.Damage, e.Position);
        }

        HashSet<int> _pendingRemoval = new();

        void OnEntityDeath(EntityDeathEvent e)
        {
            _pendingRemoval.Add(e.DeadEntityId);
        }

        void LateUpdate()
        {
            foreach (var id in _pendingRemoval)
                UnregisterEntity(id);
            _pendingRemoval.Clear();
        }

        void OnRelationChanged(FactionRelationChangedEvent e)
        {
            foreach (var (srcId, inner) in _table)
            {
                var srcFac = GetFaction(srcId);
                var toRemove = new List<int>();

                foreach (var tgtId in inner.Keys.ToList())
                {
                    if (tgtId == 0) continue; // sentinel：Sound/AllyAlert 跳过

                    var tgtFac = GetFaction(tgtId);
                    var entry = inner[tgtId];

                    if (MatchesChangedFactions(e, srcFac, tgtFac))
                    {
                        if (e.NewRelation == FactionSystem.FactionRelation.Ally)
                            toRemove.Add(tgtId);
                        else if (e.NewRelation == FactionSystem.FactionRelation.Neutral)
                        {
                            var ent = entry;
                            ent.value *= 0.5f;
                            inner[tgtId] = ent;
                        }
                    }
                }

                foreach (var id in toRemove) inner.Remove(id);
            }
        }

        bool MatchesChangedFactions(FactionRelationChangedEvent e, FactionType a, FactionType b)
            => (a == e.FactionA && b == e.FactionB) || (a == e.FactionB && b == e.FactionA);

        // ═══ 分帧 GC ═══
        void RunFrameGC()
        {
            if (_gcQueue == null || _gcQueue.Count == 0)
            {
                _gcQueue = new Queue<int>(_table.Keys);
                if (_gcQueue.Count == 0) return;
            }

            for (int i = 0; i < GC_BATCH && _gcQueue.Count > 0; i++)
            {
                int srcId = _gcQueue.Dequeue();

                if (!InstanceRegistry.Exists(srcId))
                {
                    _table.Remove(srcId);
                    continue;
                }

                if (!_table.TryGetValue(srcId, out var inner))
                    continue;

                foreach (var tgtId in inner.Keys.ToList())
                {
                    if (tgtId != 0 && !InstanceRegistry.Exists(tgtId))
                        inner.Remove(tgtId);
                }
            }
        }
    }
}
