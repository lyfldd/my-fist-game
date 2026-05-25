using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace _Game.Systems.WorldGen.Core
{
    /// <summary>
    /// Pipeline 调度器。收集所有 IGenStage，按 Order 排序后依次执行。
    /// </summary>
    public class WorldGenerator
    {
        private List<IGenStage> _stages = new();

        /// <summary> 注册一个 Stage </summary>
        public void AddStage(IGenStage stage)
        {
            _stages.Add(stage);
        }

        /// <summary> 运行整个管线 </summary>
        public void Generate(WorldData data)
        {
            var ordered = _stages
                .Where(s => s.Enabled)
                .OrderBy(s => s.Order)
                .ToList();

            foreach (var stage in ordered)
            {
                Debug.Log($"[WorldGen] Stage {stage.Order}: {stage.GetType().Name}");
                stage.Execute(data);
            }

            Debug.Log("[WorldGen] 生成完成！");
        }
    }
}
