using System.Collections.Generic;
using UnityEngine;

namespace _Game.Systems.SaveLoad
{
    /// <summary>
    /// 全局 GUID → GameObject 查找表（静态单例）。
    /// O(1) 字典查找，支持 Register/Unregister/Find/Exists/Clear。
    ///
    /// 生命周期：
    ///   游戏运行时：PersistentGUID.OnEnable → Register / OnDisable → Unregister
    ///   加载存档前：Clear() 清空所有旧条目（Phase -1）
    ///   加载存档后：由加载流程显式调用 Initialize() 注册
    /// </summary>
    public static class PersistentGUIDRegistry
    {
        private static readonly Dictionary<string, GameObject> _lookup = new Dictionary<string, GameObject>();

        public static int Count => _lookup.Count;

        public static void Register(string guid, GameObject go)
        {
            if (string.IsNullOrEmpty(guid) || go == null) return;
            _lookup[guid] = go;
        }

        public static void Unregister(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return;
            _lookup.Remove(guid);
        }

        public static GameObject Find(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return null;
            _lookup.TryGetValue(guid, out var go);
            return go;
        }

        public static bool Exists(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return false;
            return _lookup.ContainsKey(guid);
        }

        /// <summary>
        /// 清空所有注册条目。加载新存档前调用（Phase -1）。
        /// </summary>
        public static void Clear()
        {
            _lookup.Clear();
        }
    }
}
