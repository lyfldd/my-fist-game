using System.Collections.Generic;
using UnityEngine;
using _Game.Config;

namespace _Game.Systems.Crafting
{
    /// <summary>
    /// 研究中心管理器。追踪已完成的研究项目，解锁工业设备配方。
    /// </summary>
    public class ChemicalResearchManager : MonoBehaviour
    {
        [SerializeField] ChemicalResearchData _researchData;
        [SerializeField] List<string> _completedIds = new List<string>();

        public ChemicalResearchData Data => _researchData;

        void Awake()
        {
            if (_researchData == null)
                _researchData = Resources.Load<ChemicalResearchData>("ChemicalResearchData");
        }

        public bool IsResearched(string researchId) => _completedIds.Contains(researchId);

        public bool IsDeviceUnlocked(string deviceName)
        {
            if (_researchData == null) return true; // 无研究数据时全部解锁
            foreach (var proj in _researchData.projects)
            {
                if (proj.unlockedDeviceNames == null) continue;
                foreach (var dn in proj.unlockedDeviceNames)
                    if (dn == deviceName && IsResearched(proj.researchId))
                        return true;
            }
            // 不在任何研究项目中的设备默认解锁
            foreach (var proj in _researchData.projects)
            {
                if (proj.unlockedDeviceNames == null) continue;
                foreach (var dn in proj.unlockedDeviceNames)
                    if (dn == deviceName) return false;
            }
            return true;
        }

        public bool CanResearch(string researchId, Inventory.Inventory inventory)
        {
            if (_researchData == null) return false;
            var proj = FindProject(researchId);
            if (proj == null) return false;
            if (IsResearched(researchId)) return false;

            if (proj.Value.requiredIntellectLevel > 0)
            {
                var player = GameObject.FindWithTag("Player");
                if (player != null)
                {
                    var skills = player.GetComponent<Character.SurvivalXPSystem>();
                    if (skills != null && skills.GetSkillLevel(SkillType.智力) < proj.Value.requiredIntellectLevel)
                        return false;
                }
            }

            foreach (var req in proj.Value.cost)
            {
                if (req.itemData == null) continue;
                if (inventory == null || inventory.GetItemCount(req.itemData) < req.count)
                    return false;
            }
            return true;
        }

        public bool TryResearch(string researchId, Inventory.Inventory inventory)
        {
            if (!CanResearch(researchId, inventory)) return false;
            var proj = FindProject(researchId);
            if (proj == null) return false;

            foreach (var req in proj.Value.cost)
            {
                if (req.itemData == null) continue;
                inventory.RemoveItem(req.itemData, req.count);
            }
            _completedIds.Add(researchId);
            Debug.Log($"[ChemicalResearch] 研究完成: {proj.Value.displayName}，解锁设备: {string.Join(", ", proj.Value.unlockedDeviceNames)}");
            return true;
        }

        public void ForceUnlock(string researchId)
        {
            if (!_completedIds.Contains(researchId))
                _completedIds.Add(researchId);
        }

        ChemicalResearchProject? FindProject(string researchId)
        {
            if (_researchData?.projects == null) return null;
            foreach (var p in _researchData.projects)
                if (p.researchId == researchId) return p;
            return null;
        }
    }
}
