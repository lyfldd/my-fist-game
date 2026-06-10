using UnityEngine;

namespace _Game.Core
{
    /// <summary>
    /// 玩家注册表 — 唯一玩家引用来源。
    /// 只有 GameBootstrap 写入，所有其他系统只读。
    ///
    /// 用法:
    ///   var player = PlayerRegistry.Transform;
    ///   var character = PlayerRegistry.Get&lt;PlayerCharacter&gt;();
    /// </summary>
    public static class PlayerRegistry
    {
        static Transform _playerTransform;
        static GameObject _playerObject;

        /// <summary>玩家 Transform（最常用）</summary>
        public static Transform Transform
        {
            get
            {
                if (_playerTransform == null)
                {
                    var go = GameObject.FindWithTag("Player");
                    if (go != null)
                    {
                        _playerTransform = go.transform;
                        _playerObject = go;
                    }
                }
                return _playerTransform;
            }
        }

        /// <summary>玩家 GameObject</summary>
        public static GameObject GameObject => _playerObject ?? Transform?.gameObject;

        /// <summary>玩家位置（快捷）</summary>
        public static Vector3 Position => Transform != null ? Transform.position : Vector3.zero;

        /// <summary>是否已注册</summary>
        public static bool Exists => _playerTransform != null;

        /// <summary>
        /// 获取玩家身上的组件（缓存友好）
        /// </summary>
        public static T Get<T>() where T : Component
        {
            if (_playerObject == null && Transform == null) return null;
            return _playerObject.GetComponent<T>();
        }

        /// <summary>
        /// 注册玩家（仅 GameBootstrap 调用）
        /// </summary>
        public static void Register(GameObject player)
        {
            _playerObject = player;
            _playerTransform = player.transform;
        }

        /// <summary>
        /// 注销玩家
        /// </summary>
        public static void Clear()
        {
            _playerObject = null;
            _playerTransform = null;
        }
    }
}
