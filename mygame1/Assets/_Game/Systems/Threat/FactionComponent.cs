using UnityEngine;
using _Game.Config;

namespace _Game.Systems.Threat
{
    /// <summary>
    /// 阵营组件 — 挂每个实体，声明所属阵营。
    /// OnEnable 自动注册到 ThreatSystem + InstanceRegistry。
    /// </summary>
    public class FactionComponent : MonoBehaviour
    {
        [SerializeField] FactionData _factionData;
        FactionType? _directFaction;

        public FactionType Faction => _directFaction ?? (_factionData != null ? _factionData.factionType : FactionType.Neutral);
        public FactionData FactionData => _factionData;

        void OnEnable()
        {
            int id = gameObject.GetInstanceID();
            if (ThreatSystem.Instance != null)
                ThreatSystem.Instance.RegisterEntity(id, Faction);
            InstanceRegistry.Register(id, transform);
        }

        void OnDisable()
        {
            int id = gameObject.GetInstanceID();
            if (ThreatSystem.Instance != null)
                ThreatSystem.Instance.UnregisterEntity(id);
            InstanceRegistry.Unregister(id);
        }

        /// <summary>
        /// 运行时直接设置阵营（无需 SO）。会自动重新注册到 ThreatSystem。
        /// </summary>
        public void SetFaction(FactionType faction)
        {
            int id = gameObject.GetInstanceID();
            if (ThreatSystem.Instance != null)
                ThreatSystem.Instance.UnregisterEntity(id);
            _directFaction = faction;
            if (ThreatSystem.Instance != null)
                ThreatSystem.Instance.RegisterEntity(id, faction);
        }

        void Start()
        {
            if (_factionData == null && _directFaction == null)
            {
                Debug.LogWarning($"[FactionComponent] {name} 运行时未设置阵营数据！将使用 Neutral。", this);
            }
            else
            {
                Debug.Log($"[FactionComponent] {name} 阵营 = {Faction}", this);
            }
        }
    }
}
