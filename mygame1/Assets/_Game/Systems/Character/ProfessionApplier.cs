using UnityEngine;
using UnityEngine.AI;
using _Game.Config;
using _Game.Systems.Building;
using Inv = _Game.Systems.Inventory.Inventory;

namespace _Game.Systems.Character
{
    /// <summary>
    /// 职业初始物品投放组件。先穿装备（扩容容器），再放物品（填入已扩容容器）。
    /// 挂在玩家 GameObject 上，在 Start 中由 SurvivalXPSystem 触发。
    /// </summary>
    public class ProfessionApplier : MonoBehaviour
    {
        public void ApplyStartingGear(CharacterTemplate template)
        {
            if (template == null) return;

            var inv = GetComponent<Inv>();
            if (inv == null)
            {
                Debug.LogError("[ProfessionApplier] Inventory 组件缺失，无法投放初始物品");
                return;
            }

            // 第一步：穿装备 → 扩容容器
            if (template.startingEquipment != null)
            {
                foreach (var item in template.startingEquipment)
                {
                    if (item == null) continue;
                    inv.EquipItem(item);
                    Debug.Log($"[ProfessionApplier] 穿上初始装备: {item.itemName}");
                }
            }

            // 第二步：放物品 → 填入已扩容容器
            if (template.startingItems != null)
            {
                foreach (var req in template.startingItems)
                {
                    if (req.itemData == null) continue;
                    int added = inv.AddItem(req.itemData, req.count);
                    if (added > 0)
                        Debug.Log($"[ProfessionApplier] 放入初始物品: {req.itemData.itemName} ×{added}");
                }
            }

            // 第三步：特殊开局 — 通过建造系统直接放置AI机器人
            if (template.startWithAIBot && template.startingAIBotBuildable != null)
            {
                SpawnStartingAIBot(template.startingAIBotBuildable);
            }
        }

        void SpawnStartingAIBot(BuildableData buildableData)
        {
            var buildCtrl = GetComponent<BuildModeController>();
            if (buildCtrl == null)
            {
                Debug.LogError("[ProfessionApplier] BuildModeController 缺失，无法放置AI机器人");
                return;
            }

            // 在玩家周围找有效NavMesh位置
            Vector3 spawnPos = transform.position + transform.forward * 2f + Vector3.right;
            if (!NavMesh.SamplePosition(spawnPos, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            {
                spawnPos = transform.position + Vector3.right * 2f;
                if (!NavMesh.SamplePosition(spawnPos, out hit, 5f, NavMesh.AllAreas))
                {
                    Debug.LogError("[ProfessionApplier] 无法在玩家附近找到有效位置放置AI机器人");
                    return;
                }
            }

            buildCtrl.PlaceStructureDirect(buildableData, hit.position);
            Debug.Log("[ProfessionApplier] AI机器人已通过建造系统部署（制作人职业开局）");
        }
    }
}
