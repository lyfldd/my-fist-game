using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using _Game.Config;
using _Game.Core;

namespace _Game.Systems.Threat
{
    /// <summary>
    /// 阵营系统 — 管理阵营间静态关系，支持运行时变更
    /// </summary>
    public class FactionSystem : MonoBehaviour
    {
        public static FactionSystem Instance { get; private set; }

        public enum FactionRelation
        {
            Ally,
            Hostile,
            Neutral
        }

        [SerializeField] FactionData[] _defaultFactions;

        Dictionary<(FactionType, FactionType), FactionRelation> _relations = new();
        Dictionary<FactionType, FactionData> _lookup = new();

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            LoadDefaults();
        }

        // ═══ 加载默认关系 ═══
        void LoadDefaults()
        {
            _relations.Clear();
            _lookup.Clear();

            if (_defaultFactions != null)
            {
                foreach (var data in _defaultFactions)
                {
                    if (data == null) continue;
                    _lookup[data.factionType] = data;

                    foreach (var ally in data.allies)
                        SetRelationInternal(data.factionType, ally, FactionRelation.Ally);
                    foreach (var hostile in data.hostiles)
                        SetRelationInternal(data.factionType, hostile, FactionRelation.Hostile);
                }
            }

            // 运行时无 SO 时，硬编码默认阵营关系
            EnsureDefaultRelations();

            // 填充未显式声明的阵营对 → 中立
            var allTypes = System.Enum.GetValues(typeof(FactionType)) as FactionType[];
            foreach (var a in allTypes)
            {
                foreach (var b in allTypes)
                {
                    if (a == b) continue;
                    if (!_relations.ContainsKey((a, b)))
                        _relations[(a, b)] = FactionRelation.Neutral;
                }
            }
        }

        void EnsureDefaultRelations()
        {
            // 玩家阵营 — 敌对僵尸和黑恶势力
            SetDefault(FactionType.Player, FactionType.Zombie, FactionRelation.Hostile);
            SetDefault(FactionType.Player, FactionType.Bandit, FactionRelation.Hostile);
            SetDefault(FactionType.Player, FactionType.AIBot, FactionRelation.Ally);

            // 幸存者 — 同玩家
            SetDefault(FactionType.Survivor, FactionType.Zombie, FactionRelation.Hostile);
            SetDefault(FactionType.Survivor, FactionType.Bandit, FactionRelation.Hostile);
            SetDefault(FactionType.Survivor, FactionType.Player, FactionRelation.Ally);
            SetDefault(FactionType.Survivor, FactionType.AIBot, FactionRelation.Ally);

            // 僵尸 — 敌对所有人（除自己和 Neutral）
            SetDefault(FactionType.Zombie, FactionType.Player, FactionRelation.Hostile);
            SetDefault(FactionType.Zombie, FactionType.Survivor, FactionRelation.Hostile);
            SetDefault(FactionType.Zombie, FactionType.Military, FactionRelation.Hostile);
            SetDefault(FactionType.Zombie, FactionType.AIBot, FactionRelation.Hostile);

            // AI机器人 — 敌对僵尸和黑恶势力，友军玩家
            SetDefault(FactionType.AIBot, FactionType.Zombie, FactionRelation.Hostile);
            SetDefault(FactionType.AIBot, FactionType.Bandit, FactionRelation.Hostile);
            SetDefault(FactionType.AIBot, FactionType.Player, FactionRelation.Ally);

            // 黑恶势力 — 敌对所有人
            SetDefault(FactionType.Bandit, FactionType.Player, FactionRelation.Hostile);
            SetDefault(FactionType.Bandit, FactionType.Survivor, FactionRelation.Hostile);
            SetDefault(FactionType.Bandit, FactionType.Military, FactionRelation.Hostile);
            SetDefault(FactionType.Bandit, FactionType.AIBot, FactionRelation.Hostile);

            // 军方 — 敌对僵尸和黑恶势力
            SetDefault(FactionType.Military, FactionType.Zombie, FactionRelation.Hostile);
            SetDefault(FactionType.Military, FactionType.Bandit, FactionRelation.Hostile);
            SetDefault(FactionType.Military, FactionType.Player, FactionRelation.Ally);
            SetDefault(FactionType.Military, FactionType.AIBot, FactionRelation.Ally);
        }

        void SetDefault(FactionType a, FactionType b, FactionRelation relation)
        {
            if (!_relations.ContainsKey((a, b)))
                _relations[(a, b)] = relation;
        }

        void SetRelationInternal(FactionType a, FactionType b, FactionRelation relation)
        {
            _relations[(a, b)] = relation;
            _relations[(b, a)] = relation;
        }

        // ═══ 运行时查询 ═══
        public bool IsHostile(FactionType a, FactionType b)
            => _relations.TryGetValue((a, b), out var r) && r == FactionRelation.Hostile;

        public bool IsAlly(FactionType a, FactionType b)
            => _relations.TryGetValue((a, b), out var r) && r == FactionRelation.Ally;

        public bool IsNeutral(FactionType a, FactionType b)
            => !IsHostile(a, b) && !IsAlly(a, b);

        public FactionData GetFactionData(FactionType type)
            => _lookup.TryGetValue(type, out var d) ? d : null;

        // ═══ 运行时变更 ═══
        public void SetRelation(FactionType a, FactionType b, FactionRelation relation)
        {
            SetRelationInternal(a, b, relation);

            EventBus.Publish(new FactionRelationChangedEvent(a, b, relation));
        }

        public void ResetToDefault()
        {
            LoadDefaults();
        }
    }
}
