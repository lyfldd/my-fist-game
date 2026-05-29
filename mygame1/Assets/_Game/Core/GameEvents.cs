using UnityEngine;
using _Game.Config;
using _Game.Systems.Crafting;

namespace _Game.Core
{
    /// <summary>
    /// 游戏事件数据结构定义
    /// 所有事件都是 struct，遵循 EventBus 的泛型约束
    /// </summary>

    public readonly struct PlayerDied
    {
        public string KillerName { get; }
        public int KillCount { get; }

        public PlayerDied(string killerName, int killCount)
        {
            KillerName = killerName;
            KillCount = killCount;
        }
    }

    public readonly struct PlayerDamaged
    {
        public int Damage { get; }
        public string Source { get; }

        public PlayerDamaged(int damage, string source)
        {
            Damage = damage;
            Source = source;
        }
    }

    public readonly struct ItemPickedUp
    {
        public string ItemName { get; }
        public int Count { get; }

        public ItemPickedUp(string itemName, int count)
        {
            ItemName = itemName;
            Count = count;
        }
    }

    public readonly struct ZombieDied
    {
        public float X { get; }
        public float Z { get; }
        public string LootGroup { get; }

        public ZombieDied(float x, float z, string lootGroup)
        {
            X = x;
            Z = z;
            LootGroup = lootGroup;
        }
    }

    /// <summary>
    /// 背包内容发生变化时触发
    /// </summary>
    public readonly struct InventoryChanged
    {
        public string Action { get; }       // "added", "removed", "sorted"
        public string ItemName { get; }
        public int Count { get; }

        public InventoryChanged(string action, string itemName,int count=0)
        {
            Action = action;
            ItemName = itemName;
            Count = count;
        }
    }

    /// <summary>
    /// 玩家与物体发生交互时触发
    /// </summary>
    public readonly struct InteractionEvent
    {
        public string ObjectName { get; }
        public bool Success { get; }

        public InteractionEvent(string objectName, bool success)
        {
            ObjectName = objectName;
            Success = success;
        }
    }

    /// <summary>
    /// 游戏时间变化时触发（每秒多次）
    /// </summary>
    public readonly struct TimeOfDayChanged
    {
        public float CurrentHour { get; }

        public TimeOfDayChanged(float currentHour)
        {
            CurrentHour = currentHour;
        }
    }

    /// <summary>
    /// 跨天时触发（每天一次），携带新的天数编号
    /// ChunkManager 的容器刷新冷却依赖此事件判定 currentDay - openedDay > cooldownDays
    /// </summary>
    public readonly struct DayChanged
    {
        public int Day { get; }

        public DayChanged(int day)
        {
            Day = day;
        }
    }

    /// <summary>
    /// 角色属性/技能变化时触发
    /// </summary>
    public readonly struct CharacterStatsChanged
    {
        public string EventType { get; }  // "attribute_up", "skill_up", "level_up"
        public string Name { get; }
        public int NewValue { get; }

        public CharacterStatsChanged(string eventType, string name, int newValue)
        {
            EventType = eventType;
            Name = name;
            NewValue = newValue;
        }
    }

    /// <summary>
    /// 获得生存经验时触发（全系统统一发布，SurvivalXPSystem 订阅）
    /// </summary>
    public readonly struct SurvivalXpGained
    {
        public int Amount { get; }
        public string Source { get; }

        public SurvivalXpGained(int amount, string source)
        {
            Amount = amount;
            Source = source;
        }
    }

    // ===== 生存系统事件 =====

    /// <summary>
    /// 生存数值变化
    /// </summary>
    public readonly struct SurvivalStatChanged
    {
        public SurvivalStatType StatType { get; }
        public float OldValue { get; }
        public float NewValue { get; }
        public GameObject Character { get; }

        public SurvivalStatChanged(SurvivalStatType statType, float oldValue, float newValue, GameObject character)
        {
            StatType = statType;
            OldValue = oldValue;
            NewValue = newValue;
            Character = character;
        }
    }

    /// <summary>
    /// 生存状态变化（出血/感染等）
    /// </summary>
    public readonly struct SurvivalStateChanged
    {
        public SurvivalStateType StateType { get; }
        public bool IsActive { get; }
        public GameObject Character { get; }

        public SurvivalStateChanged(SurvivalStateType stateType, bool isActive, GameObject character)
        {
            StateType = stateType;
            IsActive = isActive;
            Character = character;
        }
    }

    /// <summary>
    /// 健康受损
    /// </summary>
    public readonly struct HealthDamaged
    {
        public float DamageAmount { get; }
        public string Reason { get; }
        public GameObject Character { get; }

        public HealthDamaged(float damageAmount, string reason, GameObject character)
        {
            DamageAmount = damageAmount;
            Reason = reason;
            Character = character;
        }
    }

    /// <summary>
    /// 角色死亡
    /// </summary>
    public readonly struct CharacterDeath
    {
        public GameObject Character { get; }

        public CharacterDeath(GameObject character)
        {
            Character = character;
        }
    }

    /// <summary>
    /// 物品被使用（QuickItemBar 发布，ItemUsageSystem 处理）
    /// </summary>
    public readonly struct ItemUsedEvent
    {
        public ItemData ItemData { get; }
        public int Count { get; }

        public ItemUsedEvent(ItemData itemData, int count)
        {
            ItemData = itemData;
            Count = count;
        }
    }

    /// <summary>
    /// 装备变化时触发（Inventory 发布，SurvivalSystem 订阅更新护甲缓存）
    /// </summary>
    public readonly struct EquipmentChangedEvent
    {
        public float TotalArmor { get; }
        public float TotalWarmth { get; }

        public EquipmentChangedEvent(float totalArmor, float totalWarmth = 0)
        {
            TotalArmor = totalArmor;
            TotalWarmth = totalWarmth;
        }
    }

    /// <summary>
    /// 背包数据快照更新（Inventory 发布，UI 订阅刷新面板）
    /// </summary>
    public readonly struct InventoryViewChangedEvent
    {
        public InventoryViewData ViewData { get; }

        public InventoryViewChangedEvent(InventoryViewData viewData)
        {
            ViewData = viewData;
        }
    }

    // ===== 武器系统事件 =====

    /// <summary>
    /// 武器装备到武器槽时触发（Inventory 发布，WeaponHolder 订阅生成模型）
    /// </summary>
    public readonly struct WeaponEquippedEvent
    {
        public EquipSlot Slot { get; }
        public ItemData Item { get; }

        public WeaponEquippedEvent(EquipSlot slot, ItemData item)
        {
            Slot = slot;
            Item = item;
        }
    }

    /// <summary>
    /// 武器从武器槽卸下时触发（Inventory 发布，WeaponHolder 订阅销毁模型）
    /// </summary>
    public readonly struct WeaponUnequippedEvent
    {
        public EquipSlot Slot { get; }

        public WeaponUnequippedEvent(EquipSlot slot)
        {
            Slot = slot;
        }
    }

    /// <summary>
    /// 武器槽切换时触发（WeaponSwitcher 发布，WeaponHolder/UI 订阅）
    /// </summary>
    public readonly struct WeaponSlotChangedEvent
    {
        public EquipSlot Slot { get; }
        public ItemData Weapon { get; }

        public WeaponSlotChangedEvent(EquipSlot slot, ItemData weapon)
        {
            Slot = slot;
            Weapon = weapon;
        }
    }

    /// <summary>
    /// 武器开火时触发（WeaponShooting 发布，音效/特效系统订阅）
    /// </summary>
    public readonly struct WeaponFiredEvent
    {
        public EquipSlot Slot { get; }
        public Vector3 Direction { get; }
        public bool HitSomething { get; }
        public GameObject Target { get; }

        public WeaponFiredEvent(EquipSlot slot, Vector3 direction, bool hitSomething, GameObject target)
        {
            Slot = slot;
            Direction = direction;
            HitSomething = hitSomething;
            Target = target;
        }
    }

    // ===== 建造系统事件 =====

    /// <summary>
    /// 玩家进入建造模式时触发（BuildModeController 发布，PlayerInteraction/HUD 订阅）
    /// </summary>
    public readonly struct BuildModeEnteredEvent
    {
        public BuildableData Buildable { get; }

        public BuildModeEnteredEvent(BuildableData buildable)
        {
            Buildable = buildable;
        }
    }

    /// <summary>
    /// 玩家退出建造模式时触发（BuildModeController 发布，PlayerInteraction/HUD 订阅）
    /// </summary>
    public readonly struct BuildModeExitedEvent { }

    /// <summary>
    /// 建造物放置完成时触发（BuildModeController 发布，NavMesh/存档/ZombieAI 订阅）
    /// </summary>
    public readonly struct StructurePlacedEvent
    {
        public BuildableData Buildable { get; }
        public Vector3 Position { get; }
        public GameObject Structure { get; }

        public StructurePlacedEvent(BuildableData buildable, Vector3 position, GameObject structure)
        {
            Buildable = buildable;
            Position = position;
            Structure = structure;
        }
    }

    /// <summary>
    /// 建造物被拆除时触发（PlacedStructure 发布，材料掉落/存档订阅）
    /// </summary>
    public readonly struct StructureDeconstructedEvent
    {
        public BuildableData Buildable { get; }
        public Vector3 Position { get; }
        public GameObject Structure { get; }

        public StructureDeconstructedEvent(BuildableData buildable, Vector3 position, GameObject structure)
        {
            Buildable = buildable;
            Position = position;
            Structure = structure;
        }
    }

    /// <summary>
    /// 建造进度更新（每帧发布，SurvivalSystem 订阅扣饥饿/口渴）
    /// </summary>
    public readonly struct BuildProgressTickEvent
    {
        public float HungerDrain { get; }
        public float ThirstDrain { get; }

        public BuildProgressTickEvent(float hungerDrain, float thirstDrain)
        {
            HungerDrain = hungerDrain;
            ThirstDrain = thirstDrain;
        }
    }

    // ===== 容器系统事件 =====

    /// <summary>
    /// 容器搜索完成时触发（进度条结束，物品已生成但窗口未开）
    /// 由 WorldContainer 发布，L4 区域系统可订阅追踪玩家搜刮行为
    /// </summary>
    public readonly struct ContainerSearchedEvent
    {
        public GameObject Container { get; }
        public string DisplayName { get; }

        public ContainerSearchedEvent(GameObject container, string displayName)
        {
            Container = container;
            DisplayName = displayName;
        }
    }

    /// <summary>
    /// 容器窗口打开时触发（搜索完成，UI 窗口已显示）
    /// 由 WorldContainer 发布，存档系统可订阅记录容器状态
    /// </summary>
    public readonly struct ContainerOpenedEvent
    {
        public GameObject Container { get; }
        public string DisplayName { get; }

        public ContainerOpenedEvent(GameObject container, string displayName)
        {
            Container = container;
            DisplayName = displayName;
        }
    }

    // ===== 车辆系统事件 =====

    /// <summary>
    /// 玩家上车时触发（VehicleInteraction 发布，VehicleInputLock/CameraFollow 订阅）
    /// </summary>
    public readonly struct VehicleEnteredEvent
    {
        public GameObject Vehicle { get; }
        public GameObject Driver { get; }

        public VehicleEnteredEvent(GameObject vehicle, GameObject driver)
        {
            Vehicle = vehicle;
            Driver = driver;
        }
    }

    /// <summary>
    /// 玩家下车时触发（VehicleInteraction 发布，VehicleInputLock/CameraFollow 订阅）
    /// </summary>
    public readonly struct VehicleExitedEvent
    {
        public GameObject Vehicle { get; }
        public GameObject Driver { get; }

        public VehicleExitedEvent(GameObject vehicle, GameObject driver)
        {
            Vehicle = vehicle;
            Driver = driver;
        }
    }

    // ===== 声音系统 =====

    /// <summary>
    /// 声音来源分类。监听者按此过滤感兴趣的声源。
    /// </summary>
    [System.Flags]
    public enum SoundSource
    {
        None        = 0,
        Player      = 1 << 0,
        Zombie      = 1 << 1,
        Animal      = 1 << 2,
        Vehicle     = 1 << 3,
        Environment = 1 << 4,  // 雨/雷/风
    }

    /// <summary>
    /// 声音类型标签。用于监听者进一步细分过滤。
    /// </summary>
    [System.Flags]
    public enum SoundTag
    {
        None       = 0,
        Footstep   = 1 << 0,
        Combat     = 1 << 1,
        Gunshot    = 1 << 2,
        Building   = 1 << 3,
        Voice      = 1 << 4,
        Mechanical = 1 << 5,
        Impact     = 1 << 6,
    }

    /// <summary>
    /// 声音事件。由 DecibelSystem 发出，监听者按 Source 过滤。
    /// </summary>
    public readonly struct NoiseEvent
    {
        public Vector3 Position { get; }
        public float Radius { get; }
        public SoundSource Source { get; }
        public SoundTag Tag { get; }
        public GameObject SourceObject { get; }

        public NoiseEvent(Vector3 position, float radius, SoundSource source,
                          SoundTag tag, GameObject sourceObject)
        {
            Position = position;
            Radius = radius;
            Source = source;
            Tag = tag;
            SourceObject = sourceObject;
        }
    }

    /// <summary>
    /// 合成成功事件。
    /// </summary>
    public readonly struct CraftingCompletedEvent
    {
        public string RecipeName { get; }
        public ItemData ResultItem { get; }
        public int ResultCount { get; }
        public float XpAwarded { get; }

        public CraftingCompletedEvent(string recipeName, ItemData resultItem, int resultCount, float xpAwarded)
        {
            RecipeName = recipeName;
            ResultItem = resultItem;
            ResultCount = resultCount;
            XpAwarded = xpAwarded;
        }
    }

    /// <summary>
    /// 合成失败事件。
    /// </summary>
    public readonly struct CraftingFailedEvent
    {
        public string RecipeName { get; }
        public string FailReason { get; }

        public CraftingFailedEvent(string recipeName, string failReason)
        {
            RecipeName = recipeName;
            FailReason = failReason;
        }
    }

    // ===== 生产设备事件 =====

    /// <summary>
    /// 生产设备燃料耗尽。
    /// </summary>
    public readonly struct DeviceFuelDepletedEvent
    {
        public GameObject Device { get; }

        public DeviceFuelDepletedEvent(GameObject device)
        {
            Device = device;
        }
    }

    /// <summary>
    /// 生产周期完成（输出产物）。
    /// </summary>
    public readonly struct ProductionCycleEvent
    {
        public GameObject Device { get; }
        public ItemData Output { get; }
        public int Count { get; }

        public ProductionCycleEvent(GameObject device, ItemData output, int count)
        {
            Device = device;
            Output = output;
            Count = count;
        }
    }

    /// <summary>
    /// 生产设备输出槽已满。
    /// </summary>
    public readonly struct DeviceOutputFullEvent
    {
        public GameObject Device { get; }

        public DeviceOutputFullEvent(GameObject device)
        {
            Device = device;
        }
    }

    // ===== 生产设备交互事件 =====

    /// <summary>
    /// 玩家打开生产设备面板。
    /// </summary>
    public readonly struct DeviceOpenedEvent
    {
        public ProductionDevice Device { get; }

        public DeviceOpenedEvent(ProductionDevice device)
        {
            Device = device;
        }
    }

    /// <summary>
    /// 玩家关闭生产设备面板。
    /// </summary>
    public readonly struct DeviceClosedEvent { }

    // ===== 工作台交互事件 =====

    /// <summary>
    /// 玩家打开研究中心面板。
    /// </summary>
    public readonly struct ResearchStationOpenedEvent
    {
        public WorkstationTier Tier { get; }

        public ResearchStationOpenedEvent(WorkstationTier tier)
        {
            Tier = tier;
        }
    }

    /// <summary>
    /// 玩家打开工作台合成面板。
    /// </summary>
    public readonly struct WorkstationOpenedEvent
    {
        public WorkstationTier Tier { get; }

        public WorkstationOpenedEvent(WorkstationTier tier)
        {
            Tier = tier;
        }
    }

    /// <summary>
    /// 玩家关闭工作台合成面板。
    /// </summary>
    public readonly struct WorkstationClosedEvent
    {
        public WorkstationTier Tier { get; }

        public WorkstationClosedEvent(WorkstationTier tier)
        {
            Tier = tier;
        }
    }

    // ===== AI机器人事件 =====

    /// <summary>
    /// AI机器人报废时触发。
    /// </summary>
    public readonly struct AIBotDestroyedEvent
    {
        public Vector3 Position { get; }

        public AIBotDestroyedEvent(Vector3 position)
        {
            Position = position;
        }
    }

    /// <summary>
    /// 玩家进入AI机器人驾驶模式。
    /// </summary>
    public readonly struct AIBotPilotEnteredEvent
    {
        public GameObject Bot { get; }
        public GameObject Pilot { get; }

        public AIBotPilotEnteredEvent(GameObject bot, GameObject pilot)
        {
            Bot = bot;
            Pilot = pilot;
        }
    }

    /// <summary>
    /// 玩家退出AI机器人驾驶模式。
    /// </summary>
    public readonly struct AIBotPilotExitedEvent
    {
        public GameObject Bot { get; }
        public GameObject Pilot { get; }

        public AIBotPilotExitedEvent(GameObject bot, GameObject pilot)
        {
            Bot = bot;
            Pilot = pilot;
        }
    }

    // ===== 天气事件 =====

    public readonly struct WeatherChangedEvent
    {
        public WeatherType PreviousWeather { get; }
        public WeatherType NewWeather { get; }
        public float RainIntensity { get; }
        public float Temperature { get; }

        public WeatherChangedEvent(WeatherType prev, WeatherType next, float rain, float temp)
        {
            PreviousWeather = prev;
            NewWeather = next;
            RainIntensity = rain;
            Temperature = temp;
        }
    }
}
