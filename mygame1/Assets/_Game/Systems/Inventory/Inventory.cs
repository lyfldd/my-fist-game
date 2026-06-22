using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using _Game.Config;
using _Game.Core;
using _Game.Systems.Character;

namespace _Game.Systems.Inventory
{
    /// <summary>
    /// 多容器背包组件
    /// 管理多个 InventoryContainer
    /// 总负重 = 所有容器之和
    /// </summary>
    public class Inventory : MonoBehaviour
    {
        [Header("负重设置")]
        public float maxWeight = GameConstants.INVENTORY_MAX_WEIGHT;
        public float overloadWeight = GameConstants.INVENTORY_OVERLOAD_WEIGHT;

        [Header("默认容器尺寸（无装备时）")]
        public Vector2Int defaultTopsSize = new Vector2Int(1, 1);
        public Vector2Int defaultPantsSize = new Vector2Int(2, 2);
        public Vector2Int defaultBeltSize = new Vector2Int(1, 1);
        public Vector2Int defaultVestSize = new Vector2Int(1, 1);
        public Vector2Int defaultBackpackSize = new Vector2Int(1, 1);
        public Vector2Int defaultHeadSize = new Vector2Int(1, 1);

        [Header("当前装备")]
        public Dictionary<EquipSlot, ItemData> equipped = new Dictionary<EquipSlot, ItemData>();

        [Header("容器列表（调试用）")]
        public List<InventoryContainer> containers = new List<InventoryContainer>();

        /// <summary> PlacedItem 实例 ID 自增计数器（存档系统用） </summary>
        private int _nextItemInstanceId = 1;

        /// <summary> 分配下一个实例 ID </summary>
        public int AllocateItemInstanceId() => _nextItemInstanceId++;

        /// <summary> 重置实例 ID 计数器（加载存档后调用） </summary>
        public void ResetInstanceCounter(int nextId) => _nextItemInstanceId = nextId;

        /// <summary> 获取当前计数器值 </summary>
        public int PeekNextInstanceId() => _nextItemInstanceId;

        private PlayerCharacter _playerCharacter;

        /// <summary> 力量加成后的有效负重上限 </summary>
        public float EffectiveMaxWeight
        {
            get
            {
                float bonus = _playerCharacter != null ? _playerCharacter.Strength * GameConstants.STRENGTH_CARRY_WEIGHT_BONUS : 0f;
                return maxWeight + bonus;
            }
        }

        // ===== 初始化 =====

        void OnValidate()
        {
            // 字典遍历校验，加新槽位无需改这里
            var keys = equipped.Keys.ToList();
            foreach (var slot in keys)
            {
                if (equipped[slot] != null && equipped[slot].equipSlot != slot)
                {
                    Debug.LogWarning($"不能把 {equipped[slot].itemName} 放到 {slot} 槽！");
                    equipped.Remove(slot);
                }
            }
            InitializeContainers();
        }

        void Awake()
        {
            ServiceLocator.Register(this);
            _playerCharacter = GetComponent<PlayerCharacter>();
            InitializeContainers();
        }

        void OnDestroy()
        {
            ServiceLocator.Unregister<Inventory>();
        }

        void InitializeContainers()
        {
            containers.Clear();

            // 上衣口袋（按 T 切换）
            containers.Add(new InventoryContainer
            {
                containerName = "上衣口袋",
                equipSlot = EquipSlot.Tops,
                toggleKey = KeyCode.T,
                gridWidth = defaultTopsSize.x,
                gridHeight = defaultTopsSize.y
            });

            // 裤子口袋（按 P 切换）
            containers.Add(new InventoryContainer
            {
                containerName = "裤子口袋",
                equipSlot = EquipSlot.Pants,
                toggleKey = KeyCode.P,
                gridWidth = defaultPantsSize.x,
                gridHeight = defaultPantsSize.y
            });

            containers.Add(new InventoryContainer
            {
                containerName = "腰带",
                equipSlot = EquipSlot.Belt,
                toggleKey = KeyCode.None,
                gridWidth = defaultBeltSize.x,
                gridHeight = defaultBeltSize.y
            });

            containers.Add(new InventoryContainer
            {
                containerName = "胸挂",
                equipSlot = EquipSlot.Vest,
                toggleKey = KeyCode.V,
                gridWidth = defaultVestSize.x,
                gridHeight = defaultVestSize.y
            });

            containers.Add(new InventoryContainer
            {
                containerName = "背包",
                equipSlot = EquipSlot.Backpack,
                toggleKey = KeyCode.B,
                gridWidth = defaultBackpackSize.x,
                gridHeight = defaultBackpackSize.y
            });

            // 头盔容器
            containers.Add(new InventoryContainer
            {
                containerName = "头盔",
                equipSlot = EquipSlot.Head,
                toggleKey = KeyCode.None,
                gridWidth = defaultHeadSize.x,
                gridHeight = defaultHeadSize.y
            });

            // 防弹衣容器（无容器，仅占位）
            containers.Add(new InventoryContainer
            {
                containerName = "防弹衣",
                equipSlot = EquipSlot.BodyArmor,
                toggleKey = KeyCode.None,
                gridWidth = GameConstants.BODY_ARMOR_GRID_WIDTH,
                gridHeight = GameConstants.BODY_ARMOR_GRID_HEIGHT
            });

            // 确保所有容器至少 1x1（空装备槽也显示格子）
            foreach (var c in containers)
            {
                if (c.gridWidth < 1) c.gridWidth = 1;
                if (c.gridHeight < 1) c.gridHeight = 1;
            }

            // 应用已装备的物品（字典遍历，加新槽位自动生效）
            foreach (var kv in equipped)
                if (kv.Value != null) ApplyEquip(kv.Value);
        }

        void ApplyEquip(ItemData item)
        {
            var container = GetContainer(item.equipSlot);
            if (container != null)
            {
                container.gridWidth = item.storageWidth;
                container.gridHeight = item.storageHeight;
            }
        }

        // ===== 装备系统 =====

        /// <summary> 判断是否为武器槽（无容器，武器绑定位）</summary>
        public static bool IsWeaponSlot(EquipSlot slot)
        {
            return slot == EquipSlot.RightHand || slot == EquipSlot.LeftHand
                || slot == EquipSlot.KnifeBelt || slot == EquipSlot.SidearmBelt;
        }

        /// <summary>
        /// 穿上装备到指定槽位。武器类物品按 targetSlot 装备；非武器忽略 targetSlot。
        /// 返回 true 表示装备成功。
        /// </summary>
        public bool EquipItem(ItemData item, EquipSlot targetSlot)
        {
            if (item == null || item.equipSlot == EquipSlot.None) return false;

            // 武器 → 武器槽：以拖放目标槽位为准
            var slot = (IsWeaponSlot(targetSlot) && IsWeaponSlot(item.equipSlot))
                ? targetSlot
                : item.equipSlot;

            // 小刀/手枪槽：检查腰带空间
            if (slot == EquipSlot.KnifeBelt || slot == EquipSlot.SidearmBelt)
            {
                if (!HasBeltSpaceFor(slot, 2))
                {
                    EventBus.Publish(new InventoryChanged("belt_full", item.itemName, 0));
                    return false;
                }
            }

            UnequipSlot(slot);
            equipped[slot] = item;

            if (IsWeaponSlot(slot))
            {
                if (slot == EquipSlot.KnifeBelt || slot == EquipSlot.SidearmBelt)
                    PlaceBeltGhosts(slot);

                EventBus.Publish(new WeaponEquippedEvent(slot, item));
            }
            else
            {
                var container = GetContainer(slot);
                if (container != null)
                {
                    container.gridWidth = item.storageWidth;
                    container.gridHeight = item.storageHeight;

                    if (container.placedItems.Count > 0)
                    {
                        int moved = TryMoveItemsToOtherContainers(container, slot);
                        if (moved < container.placedItems.Count)
                            Debug.LogWarning($"换装后 {container.containerName} 有物品溢出！");
                    }
                }
            }

            EventBus.Publish(new InventoryChanged("equipped", item.itemName, 0));
            EventBus.Publish(new EquipmentChangedEvent(GetTotalArmor(), GetTotalWarmth()));
            PublishView();
            return true;
        }

        /// <summary>
        /// 穿上装备到 item 自身 equipSlot
        /// </summary>
        public bool EquipItem(ItemData item)
        {
            if (item == null) return false;
            return EquipItem(item, item.equipSlot);
        }

        /// <summary> 检查腰带是否有足够空间容纳武器槽的幽灵格（考虑同槽旧武器会被卸下）</summary>
        bool HasBeltSpaceFor(EquipSlot weaponSlot, int neededCells)
        {
            var belt = GetContainer(EquipSlot.Belt);
            if (belt == null) return false;

            // 计算当前空闲格 + 该槽位现有幽灵格（将被卸下释放）
            int occupiedBySelf = 0;
            foreach (var p in belt.placedItems)
                if (p.isGhost && p.ghostSourceSlot == weaponSlot)
                    occupiedBySelf++;

            int freeCount = 0;
            for (int y = 0; y < belt.gridHeight; y++)
                for (int x = 0; x < belt.gridWidth; x++)
                {
                    bool occupied = false;
                    foreach (var p in belt.placedItems)
                    {
                        if (p.isGhost && p.ghostSourceSlot == weaponSlot) continue; // 旧幽灵不计入占用
                        if (x >= p.gridX && x < p.gridX + p.GridWidth &&
                            y >= p.gridY && y < p.gridY + p.GridHeight)
                        { occupied = true; break; }
                    }
                    if (!occupied) freeCount++;
                }

            return freeCount >= neededCells;
        }

        /// <summary> 在腰带容器中放置武器槽占位幽灵格（各占2格）</summary>
        void PlaceBeltGhosts(EquipSlot weaponSlot)
        {
            var belt = GetContainer(EquipSlot.Belt);
            if (belt == null) return;

            for (int i = 0; i < 2; i++)
            {
                // 在腰带找一个空位
                Vector2Int? free = FindFreeCell(belt, 1, 1);
                if (free.HasValue)
                {
                    belt.placedItems.Add(PlacedItem.Ghost(free.Value.x, free.Value.y, weaponSlot));
                }
                else
                {
                    Debug.LogWarning($"腰带空间不足，无法为 {weaponSlot} 放置占位格！");
                    break;
                }
            }
        }

        /// <summary> 在容器中找一个空闲位置，返回 (gridX, gridY)；无空位返回 null</summary>
        Vector2Int? FindFreeCell(InventoryContainer container, int w, int h)
        {
            for (int y = 0; y <= container.gridHeight - h; y++)
                for (int x = 0; x <= container.gridWidth - w; x++)
                    if (container.IsSpaceFree(x, y, w, h))
                        return new Vector2Int(x, y);
            return null;
        }

        /// <summary>
        /// 脱下某装备位的物品，返回脱下的物品（失败返回 null）
        /// </summary>
        public ItemData UnequipSlot(EquipSlot slot)
        {
            if (!equipped.ContainsKey(slot) || equipped[slot] == null) return null;
            var item = equipped[slot];

            if (IsWeaponSlot(slot))
            {
                // ===== 武器槽分支：移除腰带ghost + 发布武器卸下事件 =====
                RemoveBeltGhosts(slot);
                equipped.Remove(slot);
                EventBus.Publish(new WeaponUnequippedEvent(slot));
            }
            else
            {
                // ===== 普通装备槽分支：清空容器 + 恢复默认尺寸 =====
                var container = GetContainer(slot);

                if (container != null && container.placedItems.Count > 0)
                {
                    // 容器物品全部丢出（排除幽灵格）
                    var realItems = container.placedItems.FindAll(p => !p.isGhost);
                    for (int i = realItems.Count - 1; i >= 0; i--)
                        DropItem(realItems[i].itemData, realItems[i].count);
                    container.placedItems.RemoveAll(p => !p.isGhost);
                }

                // 恢复默认尺寸
                if (container != null)
                {
                    var defaultSizes = new Dictionary<EquipSlot, Vector2Int>
                    {
                        { EquipSlot.Tops, defaultTopsSize },
                        { EquipSlot.Pants, defaultPantsSize },
                        { EquipSlot.Belt, defaultBeltSize },
                        { EquipSlot.Vest, defaultVestSize },
                        { EquipSlot.Backpack, defaultBackpackSize },
                        { EquipSlot.Head, Vector2Int.one },
                        { EquipSlot.BodyArmor, new Vector2Int(GameConstants.BODY_ARMOR_GRID_WIDTH, GameConstants.BODY_ARMOR_GRID_HEIGHT) }
                    };
                    if (defaultSizes.ContainsKey(slot))
                    {
                        container.gridWidth = defaultSizes[slot].x;
                        container.gridHeight = defaultSizes[slot].y;
                    }
                }

                equipped.Remove(slot);

                // ===== 腰带拆除级联：自动卸下小刀/手枪 =====
                if (slot == EquipSlot.Belt)
                {
                    if (equipped.TryGetValue(EquipSlot.KnifeBelt, out var knifeItem) && knifeItem != null)
                    {
                        var knife = UnequipSlot(EquipSlot.KnifeBelt);
                        if (knife != null) SpawnDropAtFeet(knife);
                    }
                    if (equipped.TryGetValue(EquipSlot.SidearmBelt, out var pistolItem) && pistolItem != null)
                    {
                        var pistol = UnequipSlot(EquipSlot.SidearmBelt);
                        if (pistol != null) SpawnDropAtFeet(pistol);
                    }
                }
            }

            EventBus.Publish(new InventoryChanged("unequipped", slot.ToString(), 0));
            EventBus.Publish(new EquipmentChangedEvent(GetTotalArmor(), GetTotalWarmth()));
            PublishView();

            return item;
        }

        /// <summary> 从腰带容器移除指定武器槽的幽灵占位格</summary>
        void RemoveBeltGhosts(EquipSlot weaponSlot)
        {
            var belt = GetContainer(EquipSlot.Belt);
            if (belt == null) return;
            belt.placedItems.RemoveAll(p => p.isGhost && p.ghostSourceSlot == weaponSlot);
        }

        /// <summary> 在玩家脚下生成掉落物品</summary>
        void SpawnDropAtFeet(ItemData item)
        {
            if (item == null) return;
            var go = new GameObject($"World_{item.itemName}");
            go.transform.position = transform.position + transform.forward * GameConstants.DROP_FORWARD_OFFSET + Vector3.up * GameConstants.DROP_UP_OFFSET;
            var wi = go.AddComponent<_Game.Systems.WorldContainer.WorldItem>();
            wi.itemData = item;
            wi.count = 1;
        }

        /// <summary> 尝试把某个容器的物品移到其他容器，返回成功移走的物品数 </summary>
        int TryMoveItemsToOtherContainers(InventoryContainer source, EquipSlot excludeSlot)
        {
            int moved = 0;
            var itemsCopy = new List<PlacedItem>(source.placedItems);

            foreach (var p in itemsCopy)
            {
                // 尝试放入其他容器
                foreach (var c in containers)
                {
                    if (c.equipSlot == excludeSlot) continue;
                    if (c.TotalCells == 0) continue;

                    int added = c.AddItem(p.itemData, p.count, overloadWeight);
                    if (added > 0)
                    {
                        source.RemoveItem(p.itemData, added);
                        moved++;
                        break;
                    }
                }
            }

            return moved;
        }

        /// <summary>
        /// 根据装备位获取容器
        /// </summary>
        public InventoryContainer GetContainer(EquipSlot slot)
        {
            return containers.Find(c => c.equipSlot == slot);
        }

        /// <summary> 计算当前装备总护甲值（用于伤害减免）</summary>
        public float GetTotalArmor()
        {
            float total = 0;
            foreach (var kv in equipped)
                if (kv.Value != null) total += kv.Value.armorValue;
            return total;
        }

        public float GetTotalWarmth()
        {
            float total = 0;
            foreach (var kv in equipped)
                if (kv.Value != null) total += kv.Value.warmthValue;
            return total;
        }

        /// <summary> 构建背包数据快照（供 UI 只读渲染）</summary>
        public InventoryViewData BuildViewData()
        {
            var data = new InventoryViewData();
            data.containers = new List<ContainerViewData>();
            foreach (var c in containers)
            {
                var cv = new ContainerViewData
                {
                    containerName = c.containerName,
                    equipSlot = c.equipSlot,
                    gridWidth = c.gridWidth,
                    gridHeight = c.gridHeight,
                    items = new List<ItemOnGrid>()
                };
                foreach (var p in c.placedItems)
                    cv.items.Add(new ItemOnGrid
                    {
                        itemName = p.itemData?.itemName ?? "",
                        count = p.count,
                        gridX = p.gridX,
                        gridY = p.gridY,
                        gridWidth = p.GridWidth,
                        gridHeight = p.GridHeight,
                        icon = p.itemData?.icon,
                        itemData = p.itemData
                    });
                data.containers.Add(cv);
            }
            data.equippedNames = new Dictionary<EquipSlot, string>();
            foreach (var kv in equipped)
                data.equippedNames[kv.Key] = kv.Value?.itemName;
            data.totalArmor = GetTotalArmor();
            data.totalWarmth = GetTotalWarmth();
            data.currentWeight = CurrentWeight;
            data.maxWeight = EffectiveMaxWeight;
            data.isHardOverloaded = IsHardOverloaded;
            data.overloadRatio = OverloadRatio;
            return data;
        }

        /// <summary> 发布背包数据快照（背包变化时调用）</summary>
        public void PublishView()
        {
            EventBus.Publish(new InventoryViewChangedEvent(BuildViewData()));
        }

        // ===== 全局属性 =====

        /// <summary> 所有容器里的所有物品（兼容旧代码） </summary>
        public List<PlacedItem> placedItems
        {
            get
            {
                List<PlacedItem> all = new List<PlacedItem>();
                foreach (var c in containers)
                    all.AddRange(c.placedItems);
                return all;
            }
        }

        public float CurrentWeight
        {
            get
            {
                float total = 0f;
                foreach (var c in containers)
                    total += c.CurrentWeight;
                return total;
            }
        }

        public int UsedCells
        {
            get
            {
                int total = 0;
                foreach (var c in containers)
                    total += c.UsedCells;
                return total;
            }
        }

        public int TotalCells
        {
            get
            {
                int total = 0;
                foreach (var c in containers)
                    total += c.TotalCells;
                return total;
            }
        }

        // 兼容旧代码：看看总界面显示哪个容器的网格？
        // 默认显示第一个有空间的容器，或者可以设置一个 activeContainer
        public int gridWidth => ActiveContainer != null ? ActiveContainer.gridWidth : 0;
        public int gridHeight => ActiveContainer != null ? ActiveContainer.gridHeight : 0;

        /// <summary> UI 当前显示哪个容器 </summary>
        public InventoryContainer ActiveContainer { get; set; }

        public bool IsOverloaded => CurrentWeight > EffectiveMaxWeight;
        public bool IsHardOverloaded => CurrentWeight >= overloadWeight;

        public float OverloadRatio
        {
            get
            {
                float range = overloadWeight - maxWeight;
                if (range <= 0f) return 0f;
                return Mathf.Clamp01((CurrentWeight - maxWeight) / range);
            }
        }

        // ===== 增删查（转发到容器） =====

        /// <summary>
        /// 添加物品到背包（按优先级尝试每个容器）
        /// </summary>
        public int AddItem(ItemData item, int count)
        {
            if (item == null || count <= 0) return 0;

            if (IsHardOverloaded)
            {
                EventBus.Publish(new InventoryChanged("overload_warning", item.itemName, 0));
                return 0;
            }

            int remaining = count;

            // 按优先级尝试容器
            // 口袋 → 腰带 → 胸挂 → 背包（只尝试有空间的）
            var priority = new EquipSlot[] { EquipSlot.Pants, EquipSlot.Belt, EquipSlot.Vest, EquipSlot.Backpack };

            foreach (var slot in priority)
            {
                if (remaining <= 0) break;
                var c = GetContainer(slot);
                if (c == null || c.TotalCells == 0) continue;

                int added = c.AddItem(item, remaining, overloadWeight);
                remaining -= added;

                if (added > 0 && remaining > 0)
                {
                    // 部分放进去了，继续试下一个容器
                    continue;
                }
            }

            int totalAdded = count - remaining;
            if (totalAdded > 0)
            {
                EventBus.Publish(new InventoryChanged("added", item.itemName, totalAdded));
                PublishView();
            }
            else if (remaining == count)
                EventBus.Publish(new InventoryChanged("slot_full", item.itemName, 0));

            return totalAdded;
        }

        /// <summary>
        /// 从背包移除物品（从所有容器中查找并移除）
        /// </summary>
        public bool RemoveItem(ItemData item, int count)
        {
            if (item == null || count <= 0) return false;

            int remaining = count;

            // 遍历所有容器，逐个移除直到数量够为止
            foreach (var c in containers)
            {
                if (remaining <= 0) break;

                // 获取该容器中所有同类型物品的 PlacedItem（按反序遍历以免索引问题）
                var matchingItems = c.placedItems
                    .Select((p, idx) => new { Item = p, Index = idx })
                    .Where(x => x.Item.itemData == item)
                    .Reverse()
                    .ToList();

                foreach (var entry in matchingItems)
                {
                    if (remaining <= 0) break;

                    int toRemove = Mathf.Min(remaining, entry.Item.count);
                    c.RemoveItem(item, toRemove);
                    remaining -= toRemove;
                }
            }

            int removed = count - remaining;
            if (removed > 0)
            {
                EventBus.Publish(new InventoryChanged("removed", item.itemName, removed));
                PublishView();
            }

            return removed > 0;
        }

        public bool HasItem(ItemData item, int count = 1)
        {
            return GetItemCount(item) >= count;
        }

        public int GetItemCount(ItemData item)
        {
            int total = 0;
            foreach (var c in containers)
                total += c.GetItemCount(item);
            return total;
        }

        /// <summary> 按物品名称统计背包中的总数量（供弹药/燃料系统使用）</summary>
        public int CountItemByName(string itemName)
        {
            if (string.IsNullOrEmpty(itemName)) return 0;
            int total = 0;
            foreach (var c in containers)
                foreach (var p in c.placedItems)
                    if (!p.isGhost && p.itemData != null && p.itemData.itemName == itemName)
                        total += p.count;
            return total;
        }

        /// <summary> 按物品名称从背包中移除指定数量（返回是否成功）</summary>
        public bool RemoveItemByName(string itemName, int count)
        {
            if (string.IsNullOrEmpty(itemName) || count <= 0) return false;
            int remaining = count;
            foreach (var c in containers)
            {
                for (int i = c.placedItems.Count - 1; i >= 0; i--)
                {
                    var p = c.placedItems[i];
                    if (p.isGhost || p.itemData == null || p.itemData.itemName != itemName) continue;
                    int take = Mathf.Min(p.count, remaining);
                    p.count -= take;
                    remaining -= take;
                    if (p.count <= 0)
                        c.placedItems.RemoveAt(i);
                    if (remaining <= 0)
                    {
                        EventBus.Publish(new InventoryChanged("removed", itemName, count));
                        PublishView();
                        return true;
                    }
                }
            }
            if (remaining < count)
            {
                EventBus.Publish(new InventoryChanged("removed", itemName, count - remaining));
                PublishView();
            }
            return remaining <= 0;
        }

        /// <summary>
        /// 把物品丢在地上
        /// </summary>
        public void DropItem(ItemData item, int count = 1)
        {
            if (!RemoveItem(item, count)) return;

            var worldItem = new GameObject($"World_{item.itemName}");
            worldItem.transform.position = transform.position + transform.forward * GameConstants.DROP_MANUAL_FORWARD + Vector3.up * GameConstants.DROP_UP_OFFSET;

            var wi = worldItem.AddComponent<_Game.Systems.WorldContainer.WorldItem>();
            wi.itemData = item;
            wi.count = count;

            EventBus.Publish(new InventoryChanged("removed", item.itemName, count));
        }

        // ============================================================
        // 存档系统接口
        // ============================================================

        /// <summary> 导出背包存档数据 </summary>
        public SaveLoad.InventorySaveData GetSaveData()
        {
            var inv = new SaveLoad.InventorySaveData();

            // 7 个容器
            inv.containers = new System.Collections.Generic.List<SaveLoad.ContainerSaveData>();
            foreach (var c in containers)
            {
                var csd = new SaveLoad.ContainerSaveData
                {
                    containerName = c.containerName,
                    equipSlotName = c.equipSlot.ToString(),
                    gridWidth = c.gridWidth,
                    gridHeight = c.gridHeight,
                    slots = new System.Collections.Generic.List<SaveLoad.SlotSaveData>(),
                };

                foreach (var item in c.placedItems)
                {
                    csd.slots.Add(new SaveLoad.SlotSaveData
                    {
                        instanceId = item.instanceId,
                        itemName = (item.isGhost || item.itemData == null) ? null : item.itemData.itemName,
                        count = item.count,
                        gridX = item.gridX,
                        gridY = item.gridY,
                        rotated = item.rotated,
                        isGhost = item.isGhost,
                        ghostSourceSlot = item.isGhost ? item.ghostSourceSlot.ToString() : null,
                    });
                }
                inv.containers.Add(csd);
            }

            // 装备物品（用 instanceId 定位）
            inv.equippedItems = new System.Collections.Generic.Dictionary<string, SaveLoad.EquippedItemSaveData>();
            foreach (var kv in equipped)
            {
                if (kv.Value == null || IsWeaponSlot(kv.Key))
                    continue; // 武器槽的装备物品在各自槽位中，不在此处存

                // 找到物品在容器中的实例
                var slotName = kv.Key.ToString();
                foreach (var c in containers)
                {
                    for (int i = 0; i < c.placedItems.Count; i++)
                    {
                        var pi = c.placedItems[i];
                        if (!pi.isGhost && pi.itemData == kv.Value && pi.instanceId > 0)
                        {
                            inv.equippedItems[slotName] = new SaveLoad.EquippedItemSaveData
                            {
                                equipSlotName = slotName,
                                itemInstanceId = pi.instanceId,
                            };
                            break;
                        }
                    }
                    if (inv.equippedItems.ContainsKey(slotName)) break;
                }
            }

            // 武器信息（由 Weapon 系统在 SaveLoadManager 中补充）
            inv.ammoReserves = new System.Collections.Generic.Dictionary<string, int>();

            return inv;
        }

        /// <summary> 从存档恢复背包 </summary>
        public void RestoreFromSave(SaveLoad.InventorySaveData inv, SaveLoad.ItemCatalog itemCatalog = null)
        {
            if (inv == null || inv.containers == null) return;

            if (itemCatalog == null)
                itemCatalog = ServiceLocator.Get<SaveLoad.ItemCatalog>();
            if (itemCatalog != null)
                itemCatalog.Build();

            // 1. 清空所有容器
            foreach (var c in containers)
                c.placedItems.Clear();

            // 2. 恢复容器内容
            foreach (var csd in inv.containers)
            {
                if (!System.Enum.TryParse<EquipSlot>(csd.equipSlotName, out var slot))
                    continue;

                var container = GetContainer(slot);
                if (container == null) continue;

                container.gridWidth = csd.gridWidth;
                container.gridHeight = csd.gridHeight;
                container.placedItems.Clear();

                if (csd.slots == null) continue;

                // 收集恢复的物品 instanceId，用于后续计算最大 ID
                int maxId = 0;

                foreach (var s in csd.slots)
                {
                    if (s.isGhost)
                    {
                        if (System.Enum.TryParse<EquipSlot>(s.ghostSourceSlot, out var gs))
                        {
                            container.placedItems.Add(new PlacedItem
                            {
                                instanceId = s.instanceId,
                                itemData = null,
                                count = 0,
                                gridX = s.gridX,
                                gridY = s.gridY,
                                isGhost = true,
                                ghostSourceSlot = gs,
                            });
                        }
                    }
                    else if (!string.IsNullOrEmpty(s.itemName) && itemCatalog != null)
                    {
                        var itemData = itemCatalog.Find(s.itemName);
                        if (itemData != null)
                        {
                            container.placedItems.Add(new PlacedItem
                            {
                                instanceId = s.instanceId,
                                itemData = itemData,
                                count = s.count,
                                gridX = s.gridX,
                                gridY = s.gridY,
                                rotated = s.rotated,
                            });
                        }
                        else
                        {
                            Debug.LogWarning($"[Inventory] 恢复时找不到物品: {s.itemName}");
                        }
                    }

                    if (s.instanceId > maxId) maxId = s.instanceId;
                }

                // 更新 instanceId 计数器
                if (maxId >= _nextItemInstanceId)
                    _nextItemInstanceId = maxId + 1;
            }

            // 3. 恢复 equipped（先清空）
            var keysToRemove = new System.Collections.Generic.List<EquipSlot>();
            foreach (var kv in equipped)
                if (!IsWeaponSlot(kv.Key))
                    keysToRemove.Add(kv.Key);
            foreach (var k in keysToRemove)
                equipped.Remove(k);

            // 4. 按 instanceId 定位装备物品
            if (inv.equippedItems != null)
            {
                foreach (var kv in inv.equippedItems)
                {
                    if (!System.Enum.TryParse<EquipSlot>(kv.Key, out var equipSlot))
                        continue;

                    var targetInstanceId = kv.Value.itemInstanceId;
                    ItemData foundItem = null;

                    foreach (var c in containers)
                    {
                        foreach (var pi in c.placedItems)
                        {
                            if (!pi.isGhost && pi.instanceId == targetInstanceId && pi.itemData != null)
                            {
                                foundItem = pi.itemData;
                                break;
                            }
                        }
                        if (foundItem != null) break;
                    }

                    if (foundItem != null) // ⚠️ 注意：RestoreFromSave 不调用 EquipItem（避免副作用），
                        equipped[equipSlot] = foundItem; // 直接设置装备字典
                }
            }

            PublishView();
        }

        /// <summary> 获取指定武器槽的当前弹夹子弹数（供存档系统采集） </summary>
        public int GetWeaponAmmo(EquipSlot weaponSlot)
        {
            var ws = GetComponent<_Game.Systems.Weapon.WeaponShooting>();
            return ws != null ? ws.GetCurrentMag(weaponSlot) : 0;
        }

        /// <summary> 设置指定武器槽的当前弹夹子弹数（供存档系统恢复） </summary>
        public void SetWeaponAmmo(EquipSlot weaponSlot, int ammo)
        {
            var ws = GetComponent<_Game.Systems.Weapon.WeaponShooting>();
            if (ws != null) ws.SetCurrentMag(weaponSlot, ammo);
        }
    }
}
