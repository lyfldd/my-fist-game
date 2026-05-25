using System.Collections.Generic;
using UnityEngine;

namespace _Game.Systems.WorldGen
{
    /// <summary>
    /// 刷新调度中心 — 注册 IRefreshHandler，Chunk 加载/卸载时遍历调度。
    /// 挂载到场景单例 GameObject。
    /// </summary>
    public class RefreshHub : MonoBehaviour
    {
        public static RefreshHub Instance { get; private set; }

        private readonly List<IRefreshHandler> _handlers = new List<IRefreshHandler>();

        void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void Register(IRefreshHandler handler)
        {
            if (!_handlers.Contains(handler))
                _handlers.Add(handler);
        }

        public void OnChunkLoad(int chunkId, float currentDay)
        {
            for (int i = 0; i < _handlers.Count; i++)
                _handlers[i].OnChunkLoad(chunkId, currentDay);
        }

        public void OnChunkUnload(int chunkId)
        {
            for (int i = _handlers.Count - 1; i >= 0; i--)
                _handlers[i].OnChunkUnload(chunkId);
        }
    }
}
