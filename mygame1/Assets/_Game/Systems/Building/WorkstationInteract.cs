using UnityEngine;
using _Game.Config;
using _Game.Core;
using _Game.Systems.Crafting;
using _Game.Systems.Interaction;

namespace _Game.Systems.Building
{
    /// <summary>
    /// 工作站交互组件。挂载在放置后的工作站 GameObject 上。
    /// 实现 IInteractable：E键→设置 CraftingSystem.ActiveStation → 打开合成 UI。
    /// </summary>
    public class WorkstationInteract : MonoBehaviour, IInteractable
    {
        public WorkstationTier workstationTier;
        CraftingSystem _craftingSystem;

        void Awake()
        {
            _craftingSystem = CraftingSystem.Instance;
            if (_craftingSystem == null)
                _craftingSystem = Object.FindObjectOfType<CraftingSystem>();
        }

        string IInteractable.InteractionPrompt
        {
            get
            {
                return workstationTier switch
                {
                    WorkstationTier.Hands => "徒手制作",
                    WorkstationTier.Campfire => "使用篝火",
                    WorkstationTier.SimpleBench => "使用简易工作台",
                    WorkstationTier.Furnace => "使用熔炉",
                    WorkstationTier.MediumBench => "使用中级工作台",
                    WorkstationTier.AdvancedBench => "使用高级工作台",
                    WorkstationTier.Chemistry => "使用研究中心",
                    WorkstationTier.Machining => "使用机械加工台",
                    _ => "使用工作台"
                };
            }
        }

        float IInteractable.InteractionTime => 0.3f;
        bool IInteractable.IsInteractable => enabled && gameObject.activeInHierarchy;

        void IInteractable.OnInteract(GameObject interactor)
        {
            // 研究中心走独立研究面板，不走合成UI
            if (workstationTier == WorkstationTier.Chemistry)
            {
                EventBus.Publish(new ResearchStationOpenedEvent(workstationTier));
                Debug.Log($"[WorkstationInteract] 打开研究中心面板");
                return;
            }

            if (_craftingSystem == null)
            {
                Debug.LogError("[WorkstationInteract] CraftingSystem.Instance 为空，请确保场景中有 CraftingSystem");
                return;
            }

            _craftingSystem.ActiveStation = workstationTier;
            EventBus.Publish(new WorkstationOpenedEvent(workstationTier));

            // TODO Phase 4: 打开 CraftingUI
            Debug.Log($"[WorkstationInteract] 打开 {workstationTier} 合成面板");
        }
    }
}
