using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using _Game.Config;
using _Game.Core;
using _Game.Systems.Building;
using _Game.Systems.Character;
using _Game.Systems.Combat;
using _Game.Systems.WorldGen;
using _Game.Systems.Zombie;
using _Game.Systems.Weather;
using Inv = _Game.Systems.Inventory.Inventory;

namespace _Game.UI
{
    /// <summary>
    /// 开发者工具面板 [F1]。
    ///
    /// 四大功能：
    ///   僵尸调试 — 距离/类型/数量 + 刷新（接管原 ZombieSpawnDebugWindow.F1）
    ///   建造调试 — 自由建造开关（免材料）/ 即时建造
    ///   物品调试 — 一键给所有物品 / 搜索物品
    ///   系统调试 — XP 加点 / 体力恢复 / 技能升级
    /// </summary>
    public class DevTools : MonoBehaviour
    {
        public static DevTools Instance { get; private set; }

        // ---- tab ----
        enum Tab { Zombie, Build, Item, System, Weather }
        Tab _tab;
        bool _visible;
        public static bool IsVisible => Instance != null && Instance._visible;

        // ---- 自由建造 ----
        [HideInInspector] public bool freeBuildMode;     // 免材料
        [HideInInspector] public bool instantBuild;       // 免读条
        public static bool FreeBuildEnabled => Instance != null && Instance.freeBuildMode;
        public static bool InstantBuildEnabled => Instance != null && Instance.instantBuild;
        BuildModeController _buildCtrl;

        // ---- 僵尸 ----
        float _zDistance = 15f;
        int _zCount = 3;
        int _zTypeIndex;
        readonly List<ZombieData> _zTypes = new List<ZombieData>();
        string _zMsg;
        float _zMsgTimer;

        // ---- 物品 ----
        string _itemSearch = "";
        Vector2 _itemScroll;
        ItemData[] _allItems;
        bool _itemsLoaded;
        ItemCategory? _itemCatFilter; // null=全部, 非null=仅该分类
        Vector2 _sysScroll;

        // ---- 系统 ----
        SurvivalXPSystem _xp;

        // ---- 天气 ----
        WeatherManager _weather;
        Vector2 _weatherScroll;

        // ---- layout ----
        float _panelW = 420f;
        float _panelH = 400f;

        void Awake() { Instance = this; }
        void OnEnable() { InputRouter.BindKey(KeyCode.F1, InputPriority.Debug, ToggleVisible, this); }
        void OnDisable() { InputRouter.UnbindAll(this); }
        void OnDestroy() { if (Instance == this) Instance = null; }

        bool ToggleVisible() { _visible = !_visible; return true; }

        void Start()
        {
#if UNITY_EDITOR
            RefreshZombieTypes();
#endif
            _xp = SurvivalXPSystem.Instance;
        }

        void Update()
        {
            if (_zMsgTimer > 0f)
                _zMsgTimer -= UnityEngine.Time.deltaTime;
        }

#if UNITY_EDITOR
        void OnGUI()
        {
            if (!_visible || !Application.isPlaying) return;

            float x = (Screen.width - _panelW) * 0.5f;
            float y = (Screen.height - _panelH) * 0.5f;

            // 背景
            GUI.color = new Color(0.06f, 0.06f, 0.06f, 0.93f);
            GUI.DrawTexture(new Rect(x, y, _panelW, _panelH), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // 标题
            GUI.Label(new Rect(x + 10f, y + 6f, _panelW - 20f, 24f),
                "<b>DevTools [F1 关闭]</b>");

            // Tab 栏
            float tabY = y + 30f;
            string[] tabNames = { "僵尸", "建造", "物品", "系统", "天气" };
            float tabW = (_panelW - 20f) / 5f;
            for (int i = 0; i < 5; i++)
            {
                Rect tabRect = new Rect(x + 10f + i * tabW, tabY, tabW - 2f, 26f);
                Color prev = GUI.backgroundColor;
                GUI.backgroundColor = (int)_tab == i ? new Color(0f, 0.5f, 0f, 1f) : new Color(0.25f, 0.25f, 0.25f, 1f);
                if (GUI.Button(tabRect, tabNames[i]))
                    _tab = (Tab)i;
                GUI.backgroundColor = prev;
            }

            // 分隔线
            GUI.color = new Color(0.3f, 0.3f, 0.3f, 1f);
            GUI.DrawTexture(new Rect(x + 10f, tabY + 28f, _panelW - 20f, 1f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // 内容区
            float contentY = tabY + 34f;
            GUILayout.BeginArea(new Rect(x + 12f, contentY, _panelW - 24f, _panelH - (contentY - y) - 8f));
            GUILayout.BeginVertical();

            switch (_tab)
            {
                case Tab.Zombie: DrawZombieTab(); break;
                case Tab.Build: DrawBuildTab(); break;
                case Tab.Item: DrawItemTab(); break;
                case Tab.System: DrawSystemTab(); break;
                case Tab.Weather: DrawWeatherTab(); break;
            }

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        // ================================================================
        // 僵尸调试
        // ================================================================

        void RefreshZombieTypes()
        {
            _zTypes.Clear();
            var spawner = ZombieSpawner.Instance;
            if (spawner?.zoneProfiles == null) return;
            var seen = new HashSet<ZombieData>();
            foreach (var profile in spawner.zoneProfiles)
            {
                if (profile?.typeWeights == null) continue;
                foreach (var tw in profile.typeWeights)
                {
                    if (tw.data != null && seen.Add(tw.data))
                        _zTypes.Add(tw.data);
                }
            }
            if (_zTypes.Count > 0 && _zTypeIndex >= _zTypes.Count)
                _zTypeIndex = 0;
        }

        void DrawZombieTab()
        {
            GUILayout.Label("<b>僵尸调试</b>");

            // 距离
            GUILayout.BeginHorizontal();
            GUILayout.Label($"距离: {_zDistance:F0}m", GUILayout.Width(120));
            _zDistance = GUILayout.HorizontalSlider(_zDistance, 2f, 50f);
            GUILayout.EndHorizontal();

            // 类型
            if (_zTypes.Count > 0)
            {
                GUILayout.Label("类型:");
                string[] names = new string[_zTypes.Count];
                for (int i = 0; i < _zTypes.Count; i++)
                    names[i] = _zTypes[i].zombieName;
                int cols = Mathf.Min(names.Length, 3);
                _zTypeIndex = GUILayout.SelectionGrid(_zTypeIndex, names, cols);
            }

            // 数量
            GUILayout.BeginHorizontal();
            GUILayout.Label($"数量: {_zCount}", GUILayout.Width(120));
            _zCount = Mathf.RoundToInt(GUILayout.HorizontalSlider(_zCount, 1, 30));
            GUILayout.EndHorizontal();

            GUILayout.Space(8);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("刷新类型列表", GUILayout.Height(28)))
                RefreshZombieTypes();

            GUI.backgroundColor = new Color(0.85f, 0.25f, 0.25f);
            if (GUILayout.Button($"刷新 {_zCount} 只僵尸", GUILayout.Height(28)))
                SpawnZombies();
            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();

            if (_zMsgTimer > 0f)
                GUILayout.Label(_zMsg);
        }

        void SpawnZombies()
        {
            if (_zTypes.Count == 0) { ShowZMsg("无可用僵尸类型"); return; }

            if (!PlayerRegistry.Exists) { ShowZMsg("找不到玩家"); return; }

            ZombieData data = _zTypes[_zTypeIndex];
            Vector3 pPos = PlayerRegistry.Position;
            Vector3 fwd = PlayerRegistry.Transform.forward;
            int spawned = 0;

            for (int i = 0; i < _zCount; i++)
            {
                float angle = Random.Range(-100f, 100f);
                Vector3 dir = Quaternion.Euler(0, angle, 0) * (-fwd);
                Vector3 spawnPos = pPos + dir.normalized * _zDistance;
                if (!NavMesh.SamplePosition(spawnPos, out NavMeshHit hit, 5f, NavMesh.AllAreas))
                    continue;
                spawnPos = hit.position;

                var go = new GameObject($"DevZombie_{data.zombieName}_{spawned}");
                go.transform.position = spawnPos;
                var agent = go.AddComponent<NavMeshAgent>();
                var sm = go.AddComponent<ZombieStateMachine>();
                var dm = go.AddComponent<DamageableZombie>();
                var ctrl = go.AddComponent<ZombieController>();
                ctrl.Initialize(data);
                ZombieSpawner.BuildBody(go, data);

                int chunkId = ChunkManager.GetChunkId(spawnPos);
                ChunkManager.Instance?.RegisterZombie(sm, chunkId);
                agent.enabled = true;
                spawned++;
            }

            ShowZMsg($"已刷新 {spawned}/{_zCount} 只 {data.zombieName} (距离 {_zDistance:F0}m)");
        }

        void ShowZMsg(string m) { _zMsg = m; _zMsgTimer = 3f; }

        // ================================================================
        // 建造调试
        // ================================================================

        void DrawBuildTab()
        {
            GUILayout.Label("<b>建造调试</b>");

            bool prevFree = freeBuildMode;
            freeBuildMode = GUILayout.Toggle(freeBuildMode, "自由建造（免材料检查，免扣材料）");
            if (freeBuildMode != prevFree && _buildCtrl == null)
                _buildCtrl = ServiceLocator.Get<BuildModeController>();

            bool prevInstant = instantBuild;
            instantBuild = GUILayout.Toggle(instantBuild, "即时建造（跳过读条，0秒完成）");
            if (instantBuild != prevInstant && _buildCtrl == null)
                _buildCtrl = ServiceLocator.Get<BuildModeController>();

            if (freeBuildMode || instantBuild)
            {
                if (_buildCtrl == null) _buildCtrl = ServiceLocator.Get<BuildModeController>();
            }

            GUILayout.Space(8);

            if (GUILayout.Button("B 键进入建造模式", GUILayout.Height(30)))
            {
                GUILayout.Label("  按 B 进入建造模式后可自由放置");
                GUILayout.Label("  左键确认 / 右键或 Esc 取消");
            }

            GUILayout.Space(4);

            if (GUILayout.Button("给100个木头(建造材料)", GUILayout.Height(26)))
                GiveItem("WoodLog", 100);

            if (GUILayout.Button("给50个石头", GUILayout.Height(26)))
                GiveItem("Stone", 50);

            if (GUILayout.Button("给50个铁锭", GUILayout.Height(26)))
                GiveItem("IronIngot", 50);
        }

        // ================================================================
        // 物品调试
        // ================================================================

        void DrawItemTab()
        {
            GUILayout.Label("<b>物品调试</b>");

            if (!_itemsLoaded)
            {
                LoadAllItems();
                _itemsLoaded = true;
            }

            // 搜索
            GUILayout.BeginHorizontal();
            GUILayout.Label("搜索:", GUILayout.Width(40));
            _itemSearch = GUILayout.TextField(_itemSearch, GUILayout.Width(150));
            if (GUILayout.Button("×", GUILayout.Width(28)))
                _itemSearch = "";
            if (_itemCatFilter != null && GUILayout.Button("清除筛选", GUILayout.Width(60)))
                _itemCatFilter = null;
            GUILayout.EndHorizontal();

            // 分类筛选按钮
            GUILayout.BeginHorizontal();
            DrawCatBtn("材料", ItemCategory.RawMaterial);
            DrawCatBtn("消耗品", ItemCategory.Consumable);
            DrawCatBtn("弹药", ItemCategory.Ammo);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            DrawCatBtn("装备", ItemCategory.Equipment);
            DrawCatBtn("半成品", ItemCategory.SemiFinished);
            DrawCatBtn("工作站", ItemCategory.Workstation);
            GUILayout.EndHorizontal();

            DrawCatBtn("建筑", ItemCategory.Buildable);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("给当前筛选 ×10", GUILayout.Height(26)))
                GiveFiltered(10);
            GUI.backgroundColor = new Color(0.85f, 0.45f, 0.25f);
            if (GUILayout.Button("一键给全部 ×10", GUILayout.Height(26)))
                GiveAllItems(10);
            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();

            // 物品列表 — 填满剩余空间，不够才滚动
            if (_allItems != null && _allItems.Length > 0)
            {
                var filtered = FilterItems();
                string filterLabel = _itemCatFilter != null ? $" [{_itemCatFilter}]" : "";
                GUILayout.Label($"物品{filterLabel} ({filtered.Count}/{_allItems.Length}):");

                _itemScroll = GUILayout.BeginScrollView(_itemScroll, GUILayout.ExpandHeight(true));
                foreach (var item in filtered)
                {
                    GUILayout.BeginHorizontal();
                    string label = $"{item.itemName} [{item.category}]";
                    GUILayout.Label(label, GUILayout.Width(220));
                    if (GUILayout.Button("+1", GUILayout.Width(36)))
                        GiveItem(item, 1);
                    if (GUILayout.Button("+10", GUILayout.Width(40)))
                        GiveItem(item, 10);
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndScrollView();
            }
        }

        void DrawCatBtn(string label, ItemCategory cat)
        {
            bool active = _itemCatFilter == cat;
            Color prev = GUI.backgroundColor;
            GUI.backgroundColor = active
                ? new Color(0f, 0.5f, 0f, 1f) : new Color(0.25f, 0.25f, 0.25f, 1f);
            if (GUILayout.Button(label, GUILayout.Height(22)))
            {
                _itemCatFilter = active ? null : cat;
                _itemScroll = Vector2.zero;
            }
            GUI.backgroundColor = prev;
        }

        void GiveFiltered(int count)
        {
            var filtered = FilterItems();
            foreach (var item in filtered)
                GiveItem(item, count);
        }

        void LoadAllItems()
        {
#if UNITY_EDITOR
            var guids = UnityEditor.AssetDatabase.FindAssets("t:ItemData");
            var list = new List<ItemData>();
            foreach (var guid in guids)
            {
                string p = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var item = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemData>(p);
                if (item != null) list.Add(item);
            }
            _allItems = list.ToArray();
#else
            _allItems = new ItemData[0];
#endif
            System.Array.Sort(_allItems, (a, b) =>
            {
                int c = a.category.CompareTo(b.category);
                if (c != 0) return c;
                return string.Compare(a.itemName, b.itemName, System.StringComparison.Ordinal);
            });
        }

        List<ItemData> FilterItems()
        {
            var list = new List<ItemData>();
            if (_allItems == null) return list;
            string q = _itemSearch.Trim().ToLower();
            foreach (var item in _allItems)
            {
                if (_itemCatFilter != null && item.category != _itemCatFilter.Value)
                    continue;
                if (!string.IsNullOrEmpty(q) && !item.itemName.ToLower().Contains(q))
                    continue;
                list.Add(item);
            }
            return list;
        }

        void GiveItem(string assetName, int count)
        {
#if UNITY_EDITOR
            var inv = ServiceLocator.Get<Inv>();
            if (inv == null) { Debug.LogWarning("Inventory 未找到"); return; }
            var guids = UnityEditor.AssetDatabase.FindAssets($"t:ItemData {assetName}");
            foreach (var g in guids)
            {
                string p = UnityEditor.AssetDatabase.GUIDToAssetPath(g);
                if (System.IO.Path.GetFileNameWithoutExtension(p) == assetName)
                {
                    var item = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemData>(p);
                    if (item != null) { inv.AddItem(item, count); return; }
                }
            }
            Debug.LogWarning($"未找到物品: {assetName}");
#endif
        }

        void GiveItem(ItemData item, int count)
        {
            var inv = ServiceLocator.Get<Inv>();
            inv?.AddItem(item, count);
        }

        void GiveAllMaterials()
        {
            GiveByCategory(ItemCategory.RawMaterial, 20);
            GiveByCategory(ItemCategory.SemiFinished, 20);
        }

        void GiveAllConsumables() => GiveByCategory(ItemCategory.Consumable, 10);
        void GiveAllAmmo() => GiveByCategory(ItemCategory.Ammo, 30);
        void GiveAllWeapons()
        {
            foreach (var item in _allItems)
            {
                if (item.category == ItemCategory.Equipment
                    && (item.isFirearm || item.equipSlot == EquipSlot.RightHand
                        || item.equipSlot == EquipSlot.KnifeBelt
                        || item.equipSlot == EquipSlot.SidearmBelt))
                    GiveItem(item, 1);
            }
        }

        void GiveAllTools()
        {
            foreach (var item in _allItems)
            {
                if (item.category == ItemCategory.Equipment && !item.isFirearm
                    && item.equipSlot != EquipSlot.Head && item.equipSlot != EquipSlot.Tops
                    && item.equipSlot != EquipSlot.Pants && item.equipSlot != EquipSlot.Vest
                    && item.equipSlot != EquipSlot.Belt && item.equipSlot != EquipSlot.Backpack)
                    GiveItem(item, 1);
            }
        }

        void GiveAllWorkstations() => GiveByCategory(ItemCategory.Workstation, 2);

        // ================================================================
        // 天气调试
        // ================================================================

        void DrawWeatherTab()
        {
            if (_weather == null) _weather = WeatherManager.Instance;
            if (_weather == null) { GUILayout.Label("WeatherManager 未找到，请挂载到场景"); return; }

            var data = _weather.CurrentData;
            bool isForced = _weather.IsForceOverride;

            // 固定头部：当前状态
            GUILayout.Label("<b>天气调试</b>");
            string stateLabel = isForced ? "<color=orange>强制覆盖</color>" : "<color=green>自然轮转</color>";
            GUILayout.Label($"当前: <b>{data.displayName}</b>  |  {stateLabel}");

            // 天气强制按钮
            GUILayout.BeginHorizontal();
            string[] weatherNames = { "☀晴", "☁多云", "🌧雨", "⛈暴雨" };
            for (int i = 0; i < 4; i++)
            {
                Color prev = GUI.backgroundColor;
                bool isActive = isForced
                    ? _weather.ForcedType == (Config.WeatherType)i
                    : _weather.CurrentWeather == (Config.WeatherType)i;
                GUI.backgroundColor = isActive
                    ? new Color(0f, 0.55f, 0f, 1f) : new Color(0.25f, 0.25f, 0.25f, 1f);
                if (GUILayout.Button(weatherNames[i], GUILayout.Height(26)))
                    _weather.ForceWeather((Config.WeatherType)i);
                GUI.backgroundColor = prev;
            }
            GUILayout.EndHorizontal();

            if (isForced)
            {
                GUI.backgroundColor = new Color(0.2f, 0.7f, 0.2f);
                if (GUILayout.Button("恢复自然轮转", GUILayout.Height(22)))
                    _weather.ReleaseOverride();
                GUI.backgroundColor = Color.white;
            }

            // 难度
            GUILayout.Label("<b>难度:</b>");
            GUILayout.BeginHorizontal();
            var diffs = new[] { WeatherDifficulty.Easy, WeatherDifficulty.Normal, WeatherDifficulty.Hard };
            string[] diffNames = { "简单", "普通", "困难" };
            for (int i = 0; i < 3; i++)
            {
                Color prev = GUI.backgroundColor;
                GUI.backgroundColor = _weather.Difficulty == diffs[i]
                    ? new Color(0f, 0.5f, 0f, 1f) : new Color(0.25f, 0.25f, 0.25f, 1f);
                if (GUILayout.Button(diffNames[i], GUILayout.Height(22)))
                    _weather.SetDifficulty(diffs[i]);
                GUI.backgroundColor = prev;
            }
            GUILayout.EndHorizontal();

            // ── 可滚动的数据区 ──
            _weatherScroll = GUILayout.BeginScrollView(_weatherScroll, GUILayout.ExpandHeight(true));

            GUILayout.Label($"环境温度: {_weather.AmbientTemperature:F1}°C  雨强: {data.rainIntensity * 100f:F0}%");
            GUILayout.Label($"暴风冷却: {_weather.StormCooldownRemaining}轮  连续雨: {_weather.ConsecutiveRainCount}/{GameConstants.WEATHER_MAX_CONSECUTIVE_RAIN}");

            GUILayout.Label("<b>当前效果:</b>");
            GUILayout.Label($"  口渴速率: ×{data.thirstRateMult:F1}");
            GUILayout.Label($"  环境暗度: {data.ambientDarkness * 100f:F0}%");
            GUILayout.Label($"  日光强度: {data.sunIntensity:F1}");
            GUILayout.Label($"  日光颜色: {data.sunColor}");

            GUILayout.EndScrollView();
        }

        void GiveAllItems(int count)
        {
            if (_allItems == null) return;
            foreach (var item in _allItems)
                GiveItem(item, count);
        }

        void GiveByCategory(ItemCategory cat, int count)
        {
            if (_allItems == null) return;
            foreach (var item in _allItems)
                if (item.category == cat)
                    GiveItem(item, count);
        }

        // ================================================================
        // 系统调试
        // ================================================================

        void EnsurePoints(int needed)
        {
            while (_xp.AvailablePoints < needed)
            {
                _xp.AddXP(GameConstants.XP_PER_SKILL_POINT);
                _xp.ConvertXPToPoints();
            }
        }

        void DrawSystemTab()
        {
            if (_xp == null) _xp = SurvivalXPSystem.Instance;
            if (_xp == null) { GUILayout.Label("SurvivalXPSystem 未找到"); return; }

            // 顶部固定区
            GUILayout.Label("<b>系统调试</b>");
            GUILayout.Label($"Total XP: {_xp.TotalXP}  |  Skill Pts: {_xp.AvailablePoints}");

            // XP 按钮
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+100 XP", GUILayout.Height(24))) _xp.AddXP(100);
            if (GUILayout.Button("+500 XP", GUILayout.Height(24))) _xp.AddXP(500);
            if (GUILayout.Button("+2000 XP", GUILayout.Height(24))) _xp.AddXP(2000);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("兑换XP→点数", GUILayout.Height(24)))
            {
                int got = _xp.ConvertXPToPoints();
                Debug.Log($"兑换 {got} 技能点");
            }
            if (GUILayout.Button("+5 技能点", GUILayout.Height(24)))
            {
                _xp.AddXP(GameConstants.XP_PER_SKILL_POINT * 5);
                _xp.ConvertXPToPoints();
            }
            GUILayout.EndHorizontal();

            // 体力（顶部固定）
            var stamina = ServiceLocator.Get<StaminaSystem>();
            if (stamina != null)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"<b>体力:</b> {stamina.CurrentStamina:F0}/{stamina.MaxStamina:F0}");
                if (GUILayout.Button("回满", GUILayout.Width(50), GUILayout.Height(20)))
                    stamina.Consume(-stamina.MaxStamina);
                GUILayout.EndHorizontal();
            }

            // ── 可滚动区：属性 + 全部技能 ──
            _sysScroll = GUILayout.BeginScrollView(_sysScroll, GUILayout.ExpandHeight(true));

            GUILayout.Label("<b>属性:</b>");
            foreach (AttributeType attr in System.Enum.GetValues(typeof(AttributeType)))
                GUILayout.Label($"  {attr}: Lv{_xp.GetAttributeValue(attr)}");

            GUILayout.Label("<b>技能:</b>");
            foreach (SkillType sk in System.Enum.GetValues(typeof(SkillType)))
            {
                int lv = _xp.GetSkillLevel(sk);
                int cost = SkillCostTable.GetCost(sk, lv);
                GUILayout.BeginHorizontal();
                GUILayout.Label($"  {sk}: Lv{lv} (→{lv + 1}: {cost}pt)", GUILayout.Width(240));
                if (lv < 10 && GUILayout.Button("↑", GUILayout.Width(26), GUILayout.Height(18)))
                {
                    EnsurePoints(cost);
                    if (!_xp.SpendPoint(sk))
                        Debug.Log($"升级 {sk} 失败");
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();

            // 底部快速给经验
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("满级全技能", GUILayout.Height(22)))
            {
                _xp.AddXP(100000);
                _xp.ConvertXPToPoints();
                for (int i = 0; i < 10; i++)
                {
                    foreach (SkillType s in System.Enum.GetValues(typeof(SkillType)))
                    {
                        if (_xp.GetSkillLevel(s) < 10)
                        {
                            int c = SkillCostTable.GetCost(s, _xp.GetSkillLevel(s));
                            EnsurePoints(c);
                            _xp.SpendPoint(s);
                        }
                    }
                }
            }
            GUILayout.EndHorizontal();
        }

#endif
    }
}
