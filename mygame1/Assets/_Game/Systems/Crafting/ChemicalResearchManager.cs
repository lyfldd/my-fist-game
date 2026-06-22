using System.Collections.Generic;
using UnityEngine;
using _Game.Config;
using _Game.Core;
using _Game.UI;

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

        /// <summary>
        /// 检查配方 ID 是否已被研究解锁。不在研究项目中的配方默认解锁。
        /// </summary>
        public bool IsRecipeUnlocked(string recipeId)
        {
            if (DevTools.FreeBuildEnabled) return true;
            if (_researchData == null) return true;
            foreach (var proj in _researchData.projects)
            {
                if (proj.unlockedRecipeIds == null) continue;
                foreach (var rid in proj.unlockedRecipeIds)
                    if (rid == recipeId && IsResearched(proj.researchId))
                        return true;
            }
            // 检查该配方是否在任何研究项目中；若不在，默认解锁
            foreach (var proj in _researchData.projects)
            {
                if (proj.unlockedRecipeIds == null) continue;
                foreach (var rid in proj.unlockedRecipeIds)
                    if (rid == recipeId) return false;
            }
            return true;
        }

        public bool IsResearched(string researchId)
        {
            // 自由建造模式：所有研究视为已完成
            if (DevTools.FreeBuildEnabled) return true;
            return _completedIds.Contains(researchId);
        }

        public bool IsDeviceUnlocked(string deviceName)
        {
            // 自由建造模式：所有设备视为已解锁
            if (DevTools.FreeBuildEnabled) return true;
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
                var skills = PlayerRegistry.Get<Character.SurvivalXPSystem>();
                if (skills != null && skills.GetSkillLevel(SkillType.智力) < proj.Value.requiredIntellectLevel)
                    return false;
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
            return true;
        }

        /// <summary>
        /// 获取指定大类的所有子研究项目。
        /// </summary>
        public List<ChemicalResearchProject> GetChildProjects(string parentId)
        {
            var list = new List<ChemicalResearchProject>();
            if (_researchData?.projects == null) return list;
            foreach (var p in _researchData.projects)
                if (p.parentResearchId == parentId)
                    list.Add(p);
            return list;
        }

        /// <summary>
        /// 检查大类是否已研究（子项的前置条件）。
        /// </summary>
        public bool IsCategoryResearched(string categoryId)
        {
            return IsResearched(categoryId);
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

        // 存档系统接口
        public System.Collections.Generic.List<string> GetCompletedIds() => new System.Collections.Generic.List<string>(_completedIds);
        public void RestoreCompletedIds(System.Collections.Generic.List<string> ids)
        {
            _completedIds.Clear();
            if (ids != null) _completedIds.AddRange(ids);
        }
    }
}
