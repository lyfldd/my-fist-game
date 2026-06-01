using System.Collections.Generic;
using UnityEngine;

namespace _Game.Systems.Threat
{
    /// <summary>
    /// InstanceID → Transform 映射表
    /// 轻量静态注册表，供 ThreatSystem 和 AIAgent 查询实体位置
    /// </summary>
    public static class InstanceRegistry
    {
        static readonly Dictionary<int, Transform> _lookup = new();

        public static void Register(int instanceId, Transform transform)
        {
            _lookup[instanceId] = transform;
        }

        public static void Unregister(int instanceId)
        {
            _lookup.Remove(instanceId);
        }

        public static Transform GetTransform(int instanceId)
        {
            return _lookup.TryGetValue(instanceId, out var t) ? t : null;
        }

        public static bool Exists(int instanceId)
        {
            return _lookup.ContainsKey(instanceId);
        }

        public static int Count => _lookup.Count;
    }
}
