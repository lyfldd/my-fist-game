using System.Collections.Generic;
using _Game.Config;
using _Game.Core;
using UnityEngine;
using Inv = _Game.Systems.Inventory.Inventory;

namespace _Game.Systems.Weapon
{
    /// <summary>
    /// 武器附件挂载处理器（前置D）。
    /// 挂载到玩家/装备武器上，管理附件的挂载/拆卸。
    /// </summary>
    public class WeaponAttachmentHandler : MonoBehaviour
    {
        /// <summary> 当前挂载的附件：槽位 → (ItemData, instanceId) </summary>
        private readonly Dictionary<AttachmentSlot, (ItemData item, int instanceId)> _attachments = new();

        private Inv _inventory;
        private WeaponShooting _weaponShooting;

        public IReadOnlyDictionary<AttachmentSlot, (ItemData item, int instanceId)> Attachments => _attachments;

        void Awake()
        {
            _inventory = GetComponent<Inv>();
            _weaponShooting = GetComponent<WeaponShooting>();
        }

        // ============================================================
        // 挂载 / 拆卸
        // ============================================================

        /// <summary>
        /// 从背包中将附件挂载到对应槽位。返回是否成功。
        /// </summary>
        public bool MountAttachment(ItemData attachmentItem, int instanceId)
        {
            if (attachmentItem == null || attachmentItem.attachmentSlot == AttachmentSlot.None)
                return false;
            if (_inventory == null) return false;

            var slot = attachmentItem.attachmentSlot;

            // 检查宿主是否支持此槽位
            // （从当前装备的武器/装备读取 hostSlots）
            if (!HostSupportsSlot(slot)) return false;

            // 检查是否已挂载同槽附件
            if (_attachments.ContainsKey(slot))
                return false; // 需先拆卸旧附件

            // 从背包移除附件物品
            int removed = 0;
            foreach (var c in _inventory.containers)
            {
                for (int i = c.placedItems.Count - 1; i >= 0; i--)
                {
                    if (c.placedItems[i].instanceId == instanceId)
                    {
                        removed = c.placedItems[i].count;
                        c.placedItems.RemoveAt(i);
                        break;
                    }
                }
                if (removed > 0) break;
            }
            if (removed <= 0) return false;

            // 挂载
            _attachments[slot] = (attachmentItem, instanceId);

            // 应用附件效果
            ApplyAttachmentEffects(slot, attachmentItem);

            EventBus.Publish(new AttachmentMountedEvent(attachmentItem, slot));
            return true;
        }

        /// <summary>
        /// 拆卸指定槽位的附件，返还到背包。
        /// </summary>
        public bool UnmountAttachment(AttachmentSlot slot)
        {
            if (!_attachments.TryGetValue(slot, out var entry))
                return false;

            if (_inventory != null)
                _inventory.AddItem(entry.item, 1);

            // 移除附件效果
            RemoveAttachmentEffects(slot);

            var removed = entry;
            _attachments.Remove(slot);

            EventBus.Publish(new AttachmentUnmountedEvent(removed.item, slot));
            return true;
        }

        // ============================================================
        // 槽位检查
        // ============================================================

        bool HostSupportsSlot(AttachmentSlot slot)
        {
            // 从当前手持武器读取 hostSlots
            if (_weaponShooting != null)
            {
                var weaponData = _weaponShooting.CurrentWeaponData;
                if (weaponData != null && weaponData.hostSlots != null)
                {
                    foreach (var hs in weaponData.hostSlots)
                        if (hs == slot) return true;
                }
            }
            return false;
        }

        // ============================================================
        // 附件效果
        // ============================================================

        void ApplyAttachmentEffects(AttachmentSlot slot, ItemData attachment)
        {
            float durabilityRatio = GetAttachmentDurability(attachment);

            switch (slot)
            {
                case AttachmentSlot.Muzzle: // 消音器
                    if (_weaponShooting != null)
                    {
                        // 枪声衰减：0%耐久=无消音
                        _weaponShooting.silencerEffectiveness = durabilityRatio;
                    }
                    break;

                case AttachmentSlot.Scope: // 瞄具
                    // 瞄具效果：散布减少
                    if (_weaponShooting != null && durabilityRatio > 0.5f)
                    {
                        _weaponShooting.scopeSpreadReduction = durabilityRatio * 0.3f;
                    }
                    break;

                case AttachmentSlot.Underbarrel: // 镭射
                    // 镭射效果：腰射精度提升
                    if (_weaponShooting != null)
                    {
                        _weaponShooting.laserHipfireBonus = durabilityRatio * 0.2f;
                    }
                    break;

                case AttachmentSlot.Visor: // 夜视仪
                    // 夜视效果由专门的 NightVisionEffect 组件处理
                    break;

                case AttachmentSlot.Filter: // 防毒面具
                    // 防毒效果由毒气系统（预留）查询
                    break;
            }
        }

        void RemoveAttachmentEffects(AttachmentSlot slot)
        {
            switch (slot)
            {
                case AttachmentSlot.Muzzle:
                    if (_weaponShooting != null) _weaponShooting.silencerEffectiveness = 0f;
                    break;
                case AttachmentSlot.Scope:
                    if (_weaponShooting != null) _weaponShooting.scopeSpreadReduction = 0f;
                    break;
                case AttachmentSlot.Underbarrel:
                    if (_weaponShooting != null) _weaponShooting.laserHipfireBonus = 0f;
                    break;
            }
        }

        // ============================================================
        // 耐久查询
        // ============================================================

        float GetAttachmentDurability(ItemData attachment)
        {
            if (attachment == null || !attachment.hasDurability || attachment.maxDurability <= 0f)
                return 1f;

            if (_inventory == null) return 1f;

            // 查找附件在挂载字典中的 instanceId
            int instanceId = 0;
            foreach (var kv in _attachments)
            {
                if (kv.Value.item == attachment)
                { instanceId = kv.Value.instanceId; break; }
            }
            if (instanceId <= 0) return 1f;

            // 从背包查找（附件被移除后挂载在字典中，需通过instanceId查耐久）
            var p = _inventory.FindPlacedItem(instanceId);
            if (p == null || p.Value.itemData == null || !p.Value.itemData.hasDurability)
                return 1f;

            float cur = p.Value.itemDurability;
            float max = p.Value.itemData.maxDurability;
            if (cur <= 0f && max > 0f) return 1f;
            return max > 0f ? Mathf.Clamp01(cur / max) : 1f;
        }
    }

    // ============================================================
    // 事件
    // ============================================================

    public readonly struct AttachmentMountedEvent
    {
        public ItemData Attachment { get; }
        public AttachmentSlot Slot { get; }
        public AttachmentMountedEvent(ItemData attachment, AttachmentSlot slot)
        { Attachment = attachment; Slot = slot; }
    }

    public readonly struct AttachmentUnmountedEvent
    {
        public ItemData Attachment { get; }
        public AttachmentSlot Slot { get; }
        public AttachmentUnmountedEvent(ItemData attachment, AttachmentSlot slot)
        { Attachment = attachment; Slot = slot; }
    }
}
