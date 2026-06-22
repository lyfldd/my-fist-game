using _Game.Core;
using UnityEngine;

namespace _Game.Systems.SaveLoad
{
    /// <summary>
    /// 自动保存触发器。
    /// Phase 5 实现完整逻辑，目前仅占位骨架。
    ///
    /// 触发条件（Phase 5）:
    ///   - DayChanged 事件
    ///   - 进入车辆
    ///   - 每 N 分钟定时
    ///   - 状态脏标记（建造/拆除/搜刮后） + 时间条件
    /// </summary>
    public class SaveTriggerSystem : MonoBehaviour
    {
        [Header("自动保存间隔（分钟）")]
        [SerializeField] private float _autoSaveIntervalMinutes = 10f;
        [SerializeField] private float _dirtySaveDelaySeconds = 5f;

        private float _lastAutoSaveTime;
        private bool _isDirty;

        private SaveLoadManager _saveManager;

        void Start()
        {
            _saveManager = SaveLoadManager.Instance;
            _lastAutoSaveTime = UnityEngine.Time.unscaledTime;
        }

        void Update()
        {
            // Phase 5 实现定时自动保存 + 脏标记延迟保存
            if (UnityEngine.Time.unscaledTime - _lastAutoSaveTime > _autoSaveIntervalMinutes * 60f)
            {
                _lastAutoSaveTime = UnityEngine.Time.unscaledTime;
                _saveManager?.AutoSave();
            }
        }

        /// <summary> 标记状态有变更（建造/拆除/搜刮等事件触发） </summary>
        public void MarkDirty()
        {
            _isDirty = true;
        }
    }
}
