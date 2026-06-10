using UnityEngine;
using _Game.Config;
using _Game.Core;
using _Game.Systems.Building;
using Inv = _Game.Systems.Inventory.Inventory;

namespace _Game.Systems.AIBot
{
    /// <summary>
    /// AI机器人的建造物组件。继承 PlacedStructure 以兼容建造系统，
    /// 但覆盖交互逻辑（E键打开机器人面板而非拆除）。
    /// 放置后初始化 AIBot 组件，启动伴随AI。
    /// </summary>
    [RequireComponent(typeof(AIBot))]
    public class AIBotBuildable : PlacedStructure
    {
        private AIBot _bot;

        void Awake()
        {
            _bot = GetComponent<AIBot>();
        }

        void Start()
        {
            // 初始化血量（从 AIBot 配置同步）
            if (buildableData != null)
            {
                name = buildableData.displayName;
            }
        }

        // ============================================================
        // 覆盖交互 — E键打开AI机器人面板（Step 4 实现UI）
        // ============================================================

        public override void OnInteract(GameObject interactor)
        {
            if (_bot == null || _bot.IsDead) return;

            if (AIBotUI.IsVisible)
                AIBotUI.Hide();
            else
                AIBotUI.Show(_bot);
        }

        public override string InteractionPrompt => _bot != null && !_bot.IsDead
            ? $"管理 {buildableData?.displayName ?? "AI机器人"}"
            : "报废的AI机器人";

        public override float InteractionTime => 0f; // 瞬间打开面板

        public override bool IsInteractable => _bot != null && !_bot.IsDead && gameObject.activeInHierarchy;

        // ============================================================
        // 拆除 — 特殊返还逻辑
        // ============================================================

        public void CustomDeconstruct()
        {
            // AI机器人拆除：AI核心(50%) + 小型反应堆(50%) + 部分材料
            var inventory = ServiceLocator.Get<Inv>();
            if (inventory != null && buildableData != null && buildableData.materials != null)
            {
                foreach (var req in buildableData.materials)
                {
                    int refundCount = Mathf.Max(1, Mathf.RoundToInt(req.count * buildableData.deconstructReturnRate));
                    inventory.AddItem(req.itemData, refundCount);
                    Debug.Log($"[AIBotBuildable] 返还材料: {req.itemData.itemName} x{refundCount}");
                }
            }

            Destroy(gameObject);
        }

        // ============================================================
        // AIBot 属性访问
        // ============================================================

        public AIBot Bot => _bot;
        public bool HasBot => _bot != null;
    }
}
