using System.Linq;
using _Game.Systems.WorldContainer;
using UnityEngine;

namespace _Game.Systems.SaveLoad
{
    /// <summary>
    /// 地面物品实例 ID 管理器。
    /// 每个 WorldItem 在生成时分配一个递增的 int instanceId（全局唯一）。
    /// 存档时写 instanceId，加载时按 instanceId 查找而非坐标匹配。
    ///
    /// 挂在 Managers GameObject 上，DontDestroyOnLoad。
    /// </summary>
    public class WorldItemManager : MonoBehaviour
    {
        public static WorldItemManager Instance { get; private set; }

        private int _nextInstanceId = 1;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        /// <summary> 分配下一个实例 ID </summary>
        public int AllocateId() => _nextInstanceId++;

        /// <summary> 加载存档后重置计数器 </summary>
        public void ResetCounter(int nextId) => _nextInstanceId = nextId;

        /// <summary> 获取当前计数器值 </summary>
        public int PeekNextId() => _nextInstanceId;

        /// <summary> 加载存档后：恢复所有已保存的 ID 中的最大值+1 </summary>
        public void ResetFromSavedIds(System.Collections.Generic.List<int> savedIds)
        {
            if (savedIds != null && savedIds.Count > 0)
                _nextInstanceId = savedIds.Max() + 1;
            else
                _nextInstanceId = 1;
        }
    }
}
