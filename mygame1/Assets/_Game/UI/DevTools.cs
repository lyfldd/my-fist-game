using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
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
        ItemData[] _allItems;
        bool _itemsLoaded;
        ItemCategory? _itemCatFilter; // null=全部, 非null=仅该分类

        // ---- 系统 ----
        SurvivalXPSystem _xp;

        // ---- 天气 ----
        WeatherManager _weather;

        // ---- layout ----
        float _panelW = 420f;
        float _panelH = 400f;

        // ============================================================
        // UGUI 字段
        // ============================================================
        private GameObject _canvasGo;
        private Font _font;
        private GameObject _panelGo;
        private Text _titleText;
        private GameObject _tabBar;
        private List<Button> _tabBtns = new List<Button>();
        private RectTransform _contentRect;
        private GameObject _contentGo;
        private int _lastTab = -1;
        private int _lastBuildHash;  // 检测建造tab勾选变化
        private int _lastItemHash;   // 检测物品tab变化
        private int _lastSysHash;    // 检测系统tab变化

        // 物品tab UGUI引用
        private InputField _uguiItemSearch;
        private Text _uguiItemMsg;
        private RectTransform _uguiItemScrollContent;
        private List<GameObject> _uguiItemRows = new List<GameObject>();

        // 系统tab UGUI引用
        private Text _uguiXpLabel;
        private RectTransform _uguiSysScrollContent;
        private List<GameObject> _uguiSysRows = new List<GameObject>();

        // 僵尸tab UGUI引用
        private Slider _uguiZDistSlider;
        private Text _uguiZDistLabel;
        private Slider _uguiZCountSlider;
        private Text _uguiZCountLabel;
        private Text _uguiZMsg;
        private GameObject _uguiZTypeGrid;
        private List<Button> _uguiZTypeBtns = new List<Button>();

        // 天气tab UGUI引用
        private RectTransform _uguiWeatherScrollContent;
        private List<GameObject> _uguiWeatherRows = new List<GameObject>();

        // 建造tab UGUI引用
        private Button _freeBuildBtn; private Text _freeBuildLabel;
        private Button _instantBuildBtn; private Text _instantBuildLabel;

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
            try { CreateUGUI(); }
            catch (System.Exception e) { Debug.LogError($"[DevTools] UGUI 创建失败: {e.Message}\n{e.StackTrace}"); }
        }

        void Update()
        {
            if (_zMsgTimer > 0f)
                _zMsgTimer -= UnityEngine.Time.deltaTime;

            if (_canvasGo != null)
            {
                bool show = UIModeConfig.UseUGUI && _visible && Application.isPlaying;
                if (_canvasGo.activeSelf != show)
                    _canvasGo.SetActive(show);
                if (show && (_needsRefresh || UnityEngine.Time.frameCount - _lastRefreshFrame > 30))
                {
                    _lastRefreshFrame = UnityEngine.Time.frameCount;
                    _needsRefresh = false;
                    RefreshUGUI();
                }
            }
        }
        private bool _needsRefresh = true;
        private int _lastRefreshFrame;
        void MarkDirty() => _needsRefresh = true;

        // ================================================================
        // 僵尸调试 (shared logic)
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

        void EnsurePoints(int needed)
        {
            while (_xp.AvailablePoints < needed)
            {
                _xp.AddXP(GameConstants.XP_PER_SKILL_POINT);
                _xp.ConvertXPToPoints();
            }
        }

        // ============================================================
        // UGUI 实现
        // ============================================================
        void CreateUGUI()
        {
            _font = UGUIBuilder.DefaultFont;
            float pw = _panelW + 60f; // UGUI 稍宽
            float ph = _panelH + 60f;

            // Canvas
            _canvasGo = new GameObject("DevToolsCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = _canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            _canvasGo.SetActive(false);

            // 面板
            _panelGo = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            _panelGo.transform.SetParent(_canvasGo.transform, false);
            _panelGo.GetComponent<Image>().color = new Color(0.06f, 0.06f, 0.06f, 0.93f);
            var prt = _panelGo.GetComponent<RectTransform>();
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(pw, ph);

            // 标题
            _titleText = UguiMakeText("Title", 16, FontStyle.Bold, TextAnchor.MiddleLeft, pw - 80, 26);
            UguiAttach(_titleText, _panelGo, 12, -10, 0, 1);

            // 关闭按钮
            var closeBtn = UguiMakeSmallBtn("CloseBtn", "✕", new Color(0.8f, 0.2f, 0.2f), 30, 24);
            UguiAttach(closeBtn, _panelGo, -40, -10, 1, 1);
            closeBtn.onClick.AddListener(() => { _visible = false; });

            // 分隔线
            var line = new GameObject("Line", typeof(RectTransform), typeof(Image));
            line.transform.SetParent(_panelGo.transform, false);
            line.GetComponent<Image>().color = new Color(0.3f, 0.3f, 0.3f, 1f);
            var lrt = line.GetComponent<RectTransform>();
            lrt.anchorMin = new Vector2(0, 1); lrt.anchorMax = new Vector2(1, 1);
            lrt.pivot = new Vector2(0.5f, 1);
            lrt.anchoredPosition = new Vector2(0, -40);
            lrt.sizeDelta = new Vector2(-24, 1);

            // Tab 栏
            _tabBar = new GameObject("TabBar", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            _tabBar.transform.SetParent(_panelGo.transform, false);
            var tbrt = _tabBar.GetComponent<RectTransform>();
            tbrt.anchorMin = new Vector2(0, 1); tbrt.anchorMax = new Vector2(1, 1);
            tbrt.pivot = new Vector2(0.5f, 1);
            tbrt.anchoredPosition = new Vector2(0, -44);
            tbrt.sizeDelta = new Vector2(-24, 30);
            var hlg = _tabBar.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = 2; hlg.childForceExpandWidth = true; hlg.childForceExpandHeight = true;
            hlg.childControlWidth = true; hlg.childControlHeight = true;

            string[] tabNames = { "僵尸", "建造", "物品", "系统", "天气" };
            for (int i = 0; i < 5; i++)
            {
                int idx = i;
                var btn = UguiMakeSmallBtn($"Tab_{i}", tabNames[i], new Color(0.25f, 0.25f, 0.25f), 80, 28);
                btn.transform.SetParent(_tabBar.transform, false);
                btn.onClick.AddListener(() => { _tab = (Tab)idx; });
                _tabBtns.Add(btn);
            }

            // 内容区 ScrollRect (填充剩余空间)
            var scrollGo = new GameObject("ScrollRect", typeof(RectTransform), typeof(ScrollRect), typeof(Image), typeof(RectMask2D));
            scrollGo.transform.SetParent(_panelGo.transform, false);
            var srt = scrollGo.GetComponent<RectTransform>();
            srt.anchorMin = new Vector2(0, 0); srt.anchorMax = new Vector2(1, 1);
            srt.offsetMin = new Vector2(12, 8);
            srt.offsetMax = new Vector2(-12, -78);
            scrollGo.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.08f, 1f);

            var vp = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            vp.transform.SetParent(scrollGo.transform, false);
            UguiSetStretch(vp.GetComponent<RectTransform>());
            vp.GetComponent<Image>().color = Color.clear;

            _contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            _contentGo.transform.SetParent(vp.transform, false);
            _contentRect = _contentGo.GetComponent<RectTransform>();
            _contentRect.anchorMin = new Vector2(0, 1); _contentRect.anchorMax = new Vector2(1, 1);
            _contentRect.pivot = new Vector2(0.5f, 1);
            _contentRect.sizeDelta = new Vector2(0, 0);
            var cvlg = _contentGo.GetComponent<VerticalLayoutGroup>();
            cvlg.spacing = 4; cvlg.padding = new RectOffset(6, 6, 4, 4);
            cvlg.childForceExpandWidth = true; cvlg.childForceExpandHeight = false;
            cvlg.childControlWidth = true; cvlg.childControlHeight = false;
            var csf = _contentGo.GetComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.viewport = vp.GetComponent<RectTransform>();
            scroll.content = _contentRect;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.scrollSensitivity = 50f;
            scroll.movementType = ScrollRect.MovementType.Clamped;
        }

        void RefreshUGUI()
        {
            if (_canvasGo == null || _panelGo == null) return;

            // Tab 切换检测
            if ((int)_tab != _lastTab)
            {
                _lastTab = (int)_tab;
                RebuildTabContent();
            }

            // 更新 tab 按钮颜色
            for (int i = 0; i < _tabBtns.Count && i < 5; i++)
            {
                var img = _tabBtns[i].GetComponent<Image>();
                if (img != null)
                    img.color = (int)_tab == i ? new Color(0f, 0.5f, 0f, 1f) : new Color(0.25f, 0.25f, 0.25f, 1f);
            }

            // 每帧刷新激活tab的动态内容
            switch (_tab)
            {
                case Tab.Zombie: RefreshZombieTabUGUI(); break;
                case Tab.Build: RefreshBuildTabUGUI(); break;
                case Tab.Item: RefreshItemTabUGUI(); break;
                case Tab.System: RefreshSystemTabUGUI(); break;
                case Tab.Weather: RefreshWeatherTabUGUI(); break;
            }
        }

        void ClearContent()
        {
            if (_contentGo == null) return;
            for (int i = _contentGo.transform.childCount - 1; i >= 0; i--)
            {
                var c = _contentGo.transform.GetChild(i);
                // 不删除 VerticalLayoutGroup/ContentSizeFitter 组件自身的 GameObject 的兄弟
                Destroy(c.gameObject);
            }
            _uguiItemRows.Clear();
            _uguiSysRows.Clear();
            _uguiWeatherRows.Clear();
            _uguiZTypeBtns.Clear();
        }

        void RebuildTabContent()
        {
            ClearContent();
            switch (_tab)
            {
                case Tab.Zombie: BuildZombieTabUGUI(); break;
                case Tab.Build: BuildBuildTabUGUI(); break;
                case Tab.Item: BuildItemTabUGUI(); break;
                case Tab.System: BuildSystemTabUGUI(); break;
                case Tab.Weather: BuildWeatherTabUGUI(); break;
            }
        }

        // ============================================================
        // UGUI 僵尸 Tab
        // ============================================================
        void BuildZombieTabUGUI()
        {
            // 距离滑块
            var distRow = UguiMakeRow(_contentGo, 28);
            var distLbl = UguiMakeText("DistLbl", 13, FontStyle.Normal, TextAnchor.MiddleLeft, 120, 26);
            distLbl.transform.SetParent(distRow.transform, false);
            _uguiZDistLabel = distLbl;

            _uguiZDistSlider = UguiMakeSlider("DistSlider", 2, 50, _zDistance);
            _uguiZDistSlider.transform.SetParent(distRow.transform, false);
            var dsrt = _uguiZDistSlider.GetComponent<RectTransform>();
            dsrt.sizeDelta = new Vector2(200, 20);
            _uguiZDistSlider.onValueChanged.AddListener(v => { _zDistance = v; });

            // 类型选择
            if (_zTypes.Count > 0)
            {
                var typeLabel = UguiMakeText("TypeLbl", 13, FontStyle.Normal, TextAnchor.MiddleLeft, 60, 26);
                typeLabel.transform.SetParent(_contentGo.transform, false);

                _uguiZTypeGrid = new GameObject("TypeGrid", typeof(RectTransform), typeof(HorizontalLayoutGroup));
                _uguiZTypeGrid.transform.SetParent(_contentGo.transform, false);
                var tgrt = _uguiZTypeGrid.GetComponent<RectTransform>();
                tgrt.sizeDelta = new Vector2(_contentRect.rect.width - 12, 28);
                var tghlg = _uguiZTypeGrid.GetComponent<HorizontalLayoutGroup>();
                tghlg.spacing = 2; tghlg.childForceExpandWidth = false; tghlg.childForceExpandHeight = true;
                tghlg.childControlWidth = true; tghlg.childControlHeight = true;

                for (int i = 0; i < _zTypes.Count; i++)
                {
                    int idx = i;
                    var btn = UguiMakeSmallBtn($"ZT_{i}", _zTypes[i].zombieName, new Color(0.25f, 0.25f, 0.25f), 100, 26);
                    btn.transform.SetParent(_uguiZTypeGrid.transform, false);
                    btn.onClick.AddListener(() => _zTypeIndex = idx);
                    _uguiZTypeBtns.Add(btn);
                }
            }

            // 数量滑块
            var countRow = UguiMakeRow(_contentGo, 28);
            var countLbl = UguiMakeText("CountLbl", 13, FontStyle.Normal, TextAnchor.MiddleLeft, 120, 26);
            countLbl.transform.SetParent(countRow.transform, false);
            _uguiZCountLabel = countLbl;

            _uguiZCountSlider = UguiMakeSlider("CountSlider", 1, 30, _zCount);
            _uguiZCountSlider.transform.SetParent(countRow.transform, false);
            _uguiZCountSlider.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 20);
            _uguiZCountSlider.onValueChanged.AddListener(v => { _zCount = Mathf.RoundToInt(v); });
            _uguiZCountSlider.wholeNumbers = true;

            // 按钮行
            var btnRow = UguiMakeRow(_contentGo, 32);
            var refreshBtn = UguiMakeSmallBtn("RefreshTypes", "刷新类型列表", new Color(0.25f, 0.35f, 0.25f), 120, 30);
            refreshBtn.transform.SetParent(btnRow.transform, false);
            refreshBtn.onClick.AddListener(RefreshZombieTypes);

            var spawnBtn = UguiMakeSmallBtn("SpawnBtn", $"刷新 {_zCount} 只僵尸", new Color(0.85f, 0.25f, 0.25f), 160, 30);
            spawnBtn.transform.SetParent(btnRow.transform, false);
            spawnBtn.onClick.AddListener(SpawnZombies);

            // 消息
            _uguiZMsg = UguiMakeText("ZMsg", 12, FontStyle.Normal, TextAnchor.MiddleLeft, 400, 22);
            _uguiZMsg.transform.SetParent(_contentGo.transform, false);
        }

        void RefreshZombieTabUGUI()
        {
            _uguiZDistLabel.text = $"距离: {_zDistance:F0}m";
            _uguiZCountLabel.text = $"数量: {_zCount}";

            // 更新类型按钮颜色
            for (int i = 0; i < _uguiZTypeBtns.Count && i < _zTypes.Count; i++)
            {
                var img = _uguiZTypeBtns[i].GetComponent<Image>();
                if (img != null) img.color = i == _zTypeIndex ? new Color(0f, 0.5f, 0f, 1f) : new Color(0.25f, 0.25f, 0.25f, 1f);
            }

            _uguiZMsg.text = _zMsgTimer > 0f ? _zMsg : "";
        }

        // ============================================================
        // UGUI 建造 Tab
        // ============================================================
        void BuildBuildTabUGUI()
        {
            // 自由建造 — 用 Button 切换（比 Toggle 更可靠）
            var freeRow = UguiMakeRow(_contentGo, 28);
            _freeBuildBtn = UguiMakeBigBtn("FreeBuildBtn",
                freeBuildMode ? "✓ 自由建造（开启）" : "✗ 自由建造（关闭）",
                350, 26, out _freeBuildLabel);
            _freeBuildBtn.transform.SetParent(freeRow.transform, false);
            RefreshFreeBuildBtnColor();
            _freeBuildBtn.onClick.AddListener(() =>
            {
                freeBuildMode = !freeBuildMode;
                _freeBuildLabel.text = freeBuildMode ? "✓ 自由建造（开启）" : "✗ 自由建造（关闭）";
                RefreshFreeBuildBtnColor();
                if (freeBuildMode && _buildCtrl == null) _buildCtrl = ServiceLocator.Get<BuildModeController>();
            });

            // 即时建造 — 用 Button 切换
            var instRow = UguiMakeRow(_contentGo, 28);
            _instantBuildBtn = UguiMakeBigBtn("InstantBuildBtn",
                instantBuild ? "✓ 即时建造（开启）" : "✗ 即时建造（关闭）",
                350, 26, out _instantBuildLabel);
            _instantBuildBtn.transform.SetParent(instRow.transform, false);
            RefreshInstantBuildBtnColor();
            _instantBuildBtn.onClick.AddListener(() =>
            {
                instantBuild = !instantBuild;
                _instantBuildLabel.text = instantBuild ? "✓ 即时建造（开启）" : "✗ 即时建造（关闭）";
                RefreshInstantBuildBtnColor();
                if (instantBuild && _buildCtrl == null) _buildCtrl = ServiceLocator.Get<BuildModeController>();
            });

            // 建造模式按钮
            var enterBtn = UguiMakeSmallBtn("EnterBuild", "按 B 键进入建造模式", new Color(0.25f, 0.35f, 0.55f), 200, 34);
            enterBtn.transform.SetParent(_contentGo.transform, false);

            var helpLabel = UguiMakeText("BuildHelp", 11, FontStyle.Normal, TextAnchor.MiddleLeft, 400, 36);
            helpLabel.text = "  按 B 进入建造模式后可自由放置\n  左键确认 / 右键或 Esc 取消";
            helpLabel.transform.SetParent(_contentGo.transform, false);

            // 材料按钮
            var matBtns = new[] { ("给100个木头", "WoodLog", 100), ("给50个石头", "Stone", 50), ("给50个铁锭", "IronIngot", 50) };
            foreach (var (label, asset, count) in matBtns)
            {
                var mbtn = UguiMakeSmallBtn(label, label, new Color(0.3f, 0.3f, 0.3f), 180, 28);
                mbtn.transform.SetParent(_contentGo.transform, false);
                string a = asset; int c = count;
                mbtn.onClick.AddListener(() => GiveItem(a, c));
            }
        }

        void RefreshFreeBuildBtnColor()
        {
            if (_freeBuildBtn != null)
                _freeBuildBtn.GetComponent<Image>().color = freeBuildMode
                    ? new Color(0f, 0.65f, 0f, 1f) : new Color(0.3f, 0.3f, 0.3f, 1f);
        }
        void RefreshInstantBuildBtnColor()
        {
            if (_instantBuildBtn != null)
                _instantBuildBtn.GetComponent<Image>().color = instantBuild
                    ? new Color(0f, 0.65f, 0f, 1f) : new Color(0.3f, 0.3f, 0.3f, 1f);
        }

        void RefreshBuildTabUGUI()
        {
            // 每帧更新按钮状态（处理 F7 切换模式后重新进来等情况）
            if (_freeBuildLabel != null)
            {
                string t = freeBuildMode ? "✓ 自由建造（开启）" : "✗ 自由建造（关闭）";
                if (_freeBuildLabel.text != t) { _freeBuildLabel.text = t; RefreshFreeBuildBtnColor(); }
            }
            if (_instantBuildLabel != null)
            {
                string t = instantBuild ? "✓ 即时建造（开启）" : "✗ 即时建造（关闭）";
                if (_instantBuildLabel.text != t) { _instantBuildLabel.text = t; RefreshInstantBuildBtnColor(); }
            }
        }

        // ============================================================
        // UGUI 物品 Tab
        // ============================================================
        void BuildItemTabUGUI()
        {
            if (!_itemsLoaded) { LoadAllItems(); _itemsLoaded = true; }

            // 强制清空搜索
            _itemSearch = "";
            _itemCatFilter = null;
            _lastItemHash = -1;

            // 搜索行
            var searchRow = UguiMakeRow(_contentGo, 24);
            var searchLbl = UguiMakeText("SearchLbl", 11, FontStyle.Normal, TextAnchor.MiddleLeft, 32, 22);
            searchLbl.text = "搜索:";
            searchLbl.transform.SetParent(searchRow.transform, false);

            var inputGo = new GameObject("SearchInput", typeof(RectTransform), typeof(Image), typeof(InputField));
            inputGo.transform.SetParent(searchRow.transform, false);
            inputGo.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.15f, 1f);
            inputGo.GetComponent<RectTransform>().sizeDelta = new Vector2(120, 22);
            _uguiItemSearch = inputGo.GetComponent<InputField>();
            _uguiItemSearch.textComponent = UguiMakeInputText(inputGo);
            _uguiItemSearch.text = "";
            _uguiItemSearch.onValueChanged.RemoveAllListeners();
            _uguiItemSearch.onValueChanged.AddListener(v => { _itemSearch = v; _lastItemHash = -1; });

            var clearBtn = UguiMakeSmallBtn("ClearSearch", "×", new Color(0.35f, 0.35f, 0.35f), 22, 22);
            clearBtn.transform.SetParent(searchRow.transform, false);
            clearBtn.onClick.AddListener(() => { _itemSearch = ""; if (_uguiItemSearch != null) _uguiItemSearch.text = ""; _lastItemHash = -1; });

            var clearFilterBtn = UguiMakeSmallBtn("ClearFilter", "清除", new Color(0.35f, 0.35f, 0.35f), 44, 22);
            clearFilterBtn.transform.SetParent(searchRow.transform, false);
            clearFilterBtn.onClick.AddListener(() => { _itemCatFilter = null; _lastItemHash = -1; });

            // 分类筛选按钮 — 缩小，放一行
            var cats = new[] { ("材料", ItemCategory.RawMaterial), ("消耗", ItemCategory.Consumable), ("弹药", ItemCategory.Ammo),
                               ("装备", ItemCategory.Equipment), ("半成品", ItemCategory.SemiFinished), ("工作站", ItemCategory.Workstation),
                               ("建筑", ItemCategory.Buildable) };
            var catRow = UguiMakeRow(_contentGo, 22);
            foreach (var (label, cat) in cats)
                AddCatBtnSmall(catRow, label, cat);

            // 批量给按钮行
            var giveRow = UguiMakeRow(_contentGo, 26);
            var giveFilteredBtn = UguiMakeSmallBtn("GiveFiltered", "筛选×10", new Color(0.25f, 0.35f, 0.25f), 80, 24);
            giveFilteredBtn.transform.SetParent(giveRow.transform, false);
            giveFilteredBtn.onClick.AddListener(() => GiveFiltered(10));
            var giveAllBtn = UguiMakeSmallBtn("GiveAll", "全部×10", new Color(0.8f, 0.4f, 0.25f), 80, 24);
            giveAllBtn.transform.SetParent(giveRow.transform, false);
            giveAllBtn.onClick.AddListener(() => GiveAllItems(10));

            // 物品数量
            _uguiItemMsg = UguiMakeText("ItemMsg", 11, FontStyle.Normal, TextAnchor.MiddleLeft, 400, 18);
            _uguiItemMsg.transform.SetParent(_contentGo.transform, false);

            // 物品行容器 —— 直接用 _contentGo 的 VerticalLayoutGroup 管理，不嵌套 ScrollRect
            _uguiItemScrollContent = _contentGo.GetComponent<RectTransform>();
        }

        void AddCatBtn(GameObject row, string label, ItemCategory cat)
        {
            var btn = UguiMakeSmallBtn($"Cat_{cat}", label, new Color(0.25f, 0.25f, 0.25f), 70, 22);
            btn.transform.SetParent(row.transform, false);
            btn.onClick.AddListener(() =>
            {
                _itemCatFilter = _itemCatFilter == cat ? null : cat;
                _lastItemHash = -1;
            });
            btn.gameObject.name = $"Cat_{cat}";
        }

        void AddCatBtnSmall(GameObject row, string label, ItemCategory cat)
        {
            var btn = UguiMakeSmallBtn($"Cat_{cat}", label, new Color(0.25f, 0.25f, 0.25f), 52, 20);
            btn.transform.SetParent(row.transform, false);
            btn.onClick.AddListener(() =>
            {
                _itemCatFilter = _itemCatFilter == cat ? null : cat;
                _lastItemHash = -1;
            });
            btn.gameObject.name = $"Cat_{cat}";
            // 缩小标签字体
            var lbl = btn.GetComponentInChildren<Text>();
            if (lbl != null) lbl.fontSize = 10;
        }

        void RefreshItemTabUGUI()
        {
            if (_allItems == null || _allItems.Length == 0)
            {
                if (_uguiItemMsg != null)
                    _uguiItemMsg.text = _allItems == null ? "物品未加载" : "无物品数据";
                return;
            }

            var filtered = FilterItems();
            string flt = _itemCatFilter != null ? $" [{_itemCatFilter}]" : "";
            string msg = filtered.Count == 0 && !string.IsNullOrEmpty(_itemSearch)
                ? $"搜索 \"{_itemSearch}\" 无匹配 ({_allItems.Length}个)"
                : $"物品{flt} ({filtered.Count}/{_allItems.Length}):";
            if (_uguiItemMsg != null) _uguiItemMsg.text = msg;

            UpdateCatBtnColors(_contentGo.transform);

            int hash = filtered.Count * 31 + (_itemSearch ?? "").GetHashCode();
            if (_itemCatFilter != null) hash = hash * 17 + (int)_itemCatFilter.Value;
            if (hash == _lastItemHash) return;
            _lastItemHash = hash;

            // 清除旧行，直接用 contentGo
            foreach (var r in _uguiItemRows) Destroy(r);
            _uguiItemRows.Clear();

            float contentW = _contentRect != null ? _contentRect.rect.width - 12 : 400f;

            foreach (var item in filtered)
            {
                var row = UguiMakeRow(_contentGo, 22);
                var le = row.GetComponent<LayoutElement>();
                if (le != null) le.preferredWidth = contentW;

                var lbl = UguiMakeText("IName", 11, FontStyle.Normal, TextAnchor.MiddleLeft, contentW * 0.6f, 22);
                lbl.text = $"{item.itemName} [{item.category}]";
                lbl.transform.SetParent(row.transform, false);

                var plus1 = UguiMakeSmallBtn("+1", "+1", new Color(0.2f, 0.35f, 0.2f), 32, 20);
                plus1.transform.SetParent(row.transform, false);
                var itemRef = item;
                plus1.onClick.AddListener(() => GiveItem(itemRef, 1));

                var plus10 = UguiMakeSmallBtn("+10", "+10", new Color(0.2f, 0.35f, 0.2f), 38, 20);
                plus10.transform.SetParent(row.transform, false);
                plus10.onClick.AddListener(() => GiveItem(itemRef, 10));

                _uguiItemRows.Add(row);
            }
        }

        void UpdateCatBtnColors(Transform root)
        {
            var cats = new[] { "RawMaterial", "Consumable", "Ammo", "Equipment", "SemiFinished", "Workstation", "Buildable" };
            foreach (var cn in cats)
            {
                var found = root.Find($"Cat_{cn}");
                if (found == null)
                {
                    // 深度搜索
                    found = DeepFind(root, $"Cat_{cn}");
                }
                if (found != null)
                {
                    var img = found.GetComponent<Image>();
                    if (img != null)
                    {
                        bool active = _itemCatFilter?.ToString() == cn;
                        img.color = active ? new Color(0f, 0.5f, 0f, 1f) : new Color(0.25f, 0.25f, 0.25f, 1f);
                    }
                }
            }
        }

        Transform DeepFind(Transform root, string name)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                var c = root.GetChild(i);
                if (c.name == name) return c;
                var f = DeepFind(c, name);
                if (f != null) return f;
            }
            return null;
        }

        // ============================================================
        // UGUI 系统 Tab
        // ============================================================
        void BuildSystemTabUGUI()
        {
            if (_xp == null) _xp = SurvivalXPSystem.Instance;

            // XP 标签
            _uguiXpLabel = UguiMakeText("XpLabel", 13, FontStyle.Normal, TextAnchor.MiddleLeft, 400, 24);
            _uguiXpLabel.transform.SetParent(_contentGo.transform, false);

            // XP 按钮行
            var xpRow = UguiMakeRow(_contentGo, 28);
            foreach (var ap in new[] { ("+100 XP", 100), ("+500 XP", 500), ("+2000 XP", 2000) })
            {
                int amt = ap.Item2;
                var xpb = UguiMakeSmallBtn(ap.Item1, ap.Item1, new Color(0.3f, 0.3f, 0.3f), 70, 26);
                xpb.transform.SetParent(xpRow.transform, false);
                xpb.onClick.AddListener(() => _xp.AddXP(amt));
            }

            // 兑换行
            var convRow = UguiMakeRow(_contentGo, 28);
            var convBtn = UguiMakeSmallBtn("ConvertXP", "兑换XP→点数", new Color(0.3f, 0.3f, 0.3f), 120, 26);
            convBtn.transform.SetParent(convRow.transform, false);
            convBtn.onClick.AddListener(() => { _xp.ConvertXPToPoints(); });

            var add5Btn = UguiMakeSmallBtn("+5Pt", "+5 技能点", new Color(0.3f, 0.3f, 0.3f), 100, 26);
            add5Btn.transform.SetParent(convRow.transform, false);
            add5Btn.onClick.AddListener(() => { _xp.AddXP(GameConstants.XP_PER_SKILL_POINT * 5); _xp.ConvertXPToPoints(); });

            // 体力
            var stamina = ServiceLocator.Get<StaminaSystem>();
            if (stamina != null)
            {
                var stamRow = UguiMakeRow(_contentGo, 26);
                var stamLbl = UguiMakeText("Stamina", 13, FontStyle.Normal, TextAnchor.MiddleLeft, 200, 26);
                stamLbl.transform.SetParent(stamRow.transform, false);
                var stamRef = stamLbl; // capture for refresh
                var refillBtn = UguiMakeSmallBtn("RefillStamina", "回满", new Color(0.3f, 0.3f, 0.3f), 50, 24);
                refillBtn.transform.SetParent(stamRow.transform, false);
                var stRef = stamina;
                refillBtn.onClick.AddListener(() => stRef.Consume(-stRef.MaxStamina));
                // 存储以便刷新
                stamLbl.name = "StaminaLabel";
            }

            // 满级按钮
            var maxBtn = UguiMakeSmallBtn("MaxAll", "满级全技能", new Color(0.5f, 0.2f, 0.2f), 120, 26);
            maxBtn.transform.SetParent(_contentGo.transform, false);
            maxBtn.onClick.AddListener(() =>
            {
                _xp.AddXP(100000);
                _xp.ConvertXPToPoints();
                for (int loop = 0; loop < 10; loop++)
                    foreach (SkillType s in System.Enum.GetValues(typeof(SkillType)))
                        if (_xp.GetSkillLevel(s) < 10)
                        {
                            EnsurePoints(SkillCostTable.GetCost(s, _xp.GetSkillLevel(s)));
                            _xp.SpendPoint(s);
                        }
            });

            // 可滚动区：属性 + 技能
            var attrLbl = UguiMakeText("AttrHeader", 13, FontStyle.Bold, TextAnchor.MiddleLeft, 200, 22);
            attrLbl.text = "属性:";
            attrLbl.transform.SetParent(_contentGo.transform, false);

            var sysScroll = MakeInnerScrollRect(_contentGo, 440, 180, out _uguiSysScrollContent);
            var svlg = _uguiSysScrollContent.GetComponent<VerticalLayoutGroup>();
            if (svlg == null)
            {
                svlg = _uguiSysScrollContent.gameObject.AddComponent<VerticalLayoutGroup>();
                svlg.spacing = 1; svlg.childForceExpandWidth = true; svlg.childForceExpandHeight = false;
            }
        }

        void RefreshSystemTabUGUI()
        {
            if (_xp == null) { _xp = SurvivalXPSystem.Instance; return; }
            if (_xp == null) return;

            _uguiXpLabel.text = $"Total XP: {_xp.TotalXP}  |  Skill Pts: {_xp.AvailablePoints}";

            // 体力标签
            var stamina = ServiceLocator.Get<StaminaSystem>();
            var stamLbl = DeepFind(_contentGo.transform, "StaminaLabel");
            if (stamLbl != null && stamina != null)
            {
                var t = stamLbl.GetComponent<Text>();
                if (t != null) t.text = $"<b>体力:</b> {stamina.CurrentStamina:F0}/{stamina.MaxStamina:F0}";
            }

            // 哈希检测
            int hash = _xp.TotalXP * 31 + _xp.AvailablePoints;
            if (hash == _lastSysHash) return;
            _lastSysHash = hash;

            // 重建属性和技能行
            foreach (var r in _uguiSysRows) Destroy(r);
            _uguiSysRows.Clear();
            if (_uguiSysScrollContent == null) return;

            // 属性
            foreach (AttributeType attr in System.Enum.GetValues(typeof(AttributeType)))
            {
                var lbl = UguiMakeText($"Attr_{attr}", 12, FontStyle.Normal, TextAnchor.MiddleLeft, 300, 18);
                lbl.text = $"  {attr}: Lv{_xp.GetAttributeValue(attr)}";
                lbl.transform.SetParent(_uguiSysScrollContent, false);
                _uguiSysRows.Add(lbl.gameObject);
            }

            // 技能
            var skillHeader = UguiMakeText("SkillHeader", 13, FontStyle.Bold, TextAnchor.MiddleLeft, 300, 22);
            skillHeader.text = "技能:";
            skillHeader.transform.SetParent(_uguiSysScrollContent, false);
            _uguiSysRows.Add(skillHeader.gameObject);

            foreach (SkillType sk in System.Enum.GetValues(typeof(SkillType)))
            {
                int lv = _xp.GetSkillLevel(sk);
                int cost = SkillCostTable.GetCost(sk, lv);
                var row = UguiMakeRow(_uguiSysScrollContent.gameObject, 20);
                var lbl = UguiMakeText($"Sk_{sk}", 12, FontStyle.Normal, TextAnchor.MiddleLeft, 240, 20);
                lbl.text = $"  {sk}: Lv{lv} (→{lv + 1}: {cost}pt)";
                lbl.transform.SetParent(row.transform, false);

                if (lv < 10)
                {
                    var upgradeBtn = UguiMakeSmallBtn($"Up_{sk}", "↑", new Color(0.3f, 0.3f, 0.3f), 24, 18);
                    upgradeBtn.transform.SetParent(row.transform, false);
                    var skRef = sk;
                    var cRef = cost;
                    upgradeBtn.onClick.AddListener(() =>
                    {
                        EnsurePoints(cRef);
                        _xp.SpendPoint(skRef);
                    });
                }
                _uguiSysRows.Add(row);
            }
        }

        // ============================================================
        // UGUI 天气 Tab
        // ============================================================
        void BuildWeatherTabUGUI()
        {
            if (_weather == null) _weather = WeatherManager.Instance;
            if (_weather == null) return;

            // 当前状态标签
            var statusLbl = UguiMakeText("WeatherStatus", 13, FontStyle.Normal, TextAnchor.MiddleLeft, 400, 26);
            statusLbl.transform.SetParent(_contentGo.transform, false);
            statusLbl.name = "WeatherStatusLabel";

            // 天气强制按钮
            var wRow = UguiMakeRow(_contentGo, 28);
            string[] weatherNames = { "☀晴", "☁多云", "🌧雨", "⛈暴雨" };
            for (int i = 0; i < 4; i++)
            {
                int wi = i;
                var btn = UguiMakeSmallBtn($"W_{i}", weatherNames[i], new Color(0.25f, 0.25f, 0.25f), 70, 26);
                btn.transform.SetParent(wRow.transform, false);
                btn.onClick.AddListener(() => _weather.ForceWeather((Config.WeatherType)wi));
            }

            // 恢复自然轮转
            var restoreBtn = UguiMakeSmallBtn("RestoreWeather", "恢复自然轮转", new Color(0.2f, 0.7f, 0.2f), 120, 24);
            restoreBtn.transform.SetParent(_contentGo.transform, false);
            restoreBtn.onClick.AddListener(() => _weather.ReleaseOverride());

            // 难度
            var diffLbl = UguiMakeText("DiffHeader", 13, FontStyle.Bold, TextAnchor.MiddleLeft, 80, 22);
            diffLbl.text = "难度:";
            diffLbl.transform.SetParent(_contentGo.transform, false);

            var diffRow = UguiMakeRow(_contentGo, 26);
            var diffs = new[] { WeatherDifficulty.Easy, WeatherDifficulty.Normal, WeatherDifficulty.Hard };
            string[] diffNames = { "简单", "普通", "困难" };
            for (int i = 0; i < 3; i++)
            {
                int di = i;
                var db = UguiMakeSmallBtn($"Diff_{i}", diffNames[i], new Color(0.25f, 0.25f, 0.25f), 60, 24);
                db.transform.SetParent(diffRow.transform, false);
                db.onClick.AddListener(() => _weather.SetDifficulty(diffs[di]));
            }

            // 数据滚动区
            var dataScroll = MakeInnerScrollRect(_contentGo, 440, 160, out _uguiWeatherScrollContent);
            var wvlg = _uguiWeatherScrollContent.GetComponent<VerticalLayoutGroup>();
            if (wvlg == null)
            {
                wvlg = _uguiWeatherScrollContent.gameObject.AddComponent<VerticalLayoutGroup>();
                wvlg.spacing = 1; wvlg.childForceExpandWidth = true; wvlg.childForceExpandHeight = false;
            }
        }

        void RefreshWeatherTabUGUI()
        {
            if (_weather == null) { _weather = WeatherManager.Instance; return; }
            if (_weather == null) return;

            // 更新状态标签
            var statusLbl = DeepFind(_contentGo.transform, "WeatherStatusLabel");
            if (statusLbl != null)
            {
                var t = statusLbl.GetComponent<Text>();
                var wdata = _weather.CurrentData;
                bool isForced = _weather.IsForceOverride;
                string stateLabel = isForced ? "<color=orange>强制覆盖</color>" : "<color=green>自然轮转</color>";
                if (t != null) t.text = $"当前: <b>{wdata.displayName}</b>  |  {stateLabel}  |  环境温度: {_weather.AmbientTemperature:F1}°C";
            }

            // 更新天气按钮颜色
            for (int i = 0; i < 4; i++)
            {
                var found = DeepFind(_contentGo.transform, $"W_{i}");
                if (found != null)
                {
                    var img = found.GetComponent<Image>();
                    if (img != null)
                    {
                        bool active = _weather.IsForceOverride
                            ? _weather.ForcedType == (Config.WeatherType)i
                            : _weather.CurrentWeather == (Config.WeatherType)i;
                        img.color = active ? new Color(0f, 0.55f, 0f, 1f) : new Color(0.25f, 0.25f, 0.25f, 1f);
                    }
                }
            }

            // 更新难度按钮颜色
            var diffs = new[] { WeatherDifficulty.Easy, WeatherDifficulty.Normal, WeatherDifficulty.Hard };
            for (int i = 0; i < 3; i++)
            {
                var found = DeepFind(_contentGo.transform, $"Diff_{i}");
                if (found != null)
                {
                    var img = found.GetComponent<Image>();
                    if (img != null)
                        img.color = _weather.Difficulty == diffs[i] ? new Color(0f, 0.5f, 0f, 1f) : new Color(0.25f, 0.25f, 0.25f, 1f);
                }
            }

            // 更新数据区
            var data = _weather.CurrentData;
            foreach (var r in _uguiWeatherRows) Destroy(r);
            _uguiWeatherRows.Clear();
            if (_uguiWeatherScrollContent == null) return;

            string[] lines = {
                $"雨强: {data.rainIntensity * 100f:F0}%  暴风冷却: {_weather.StormCooldownRemaining}轮",
                $"连续雨: {_weather.ConsecutiveRainCount}/{GameConstants.WEATHER_MAX_CONSECUTIVE_RAIN}",
                $"口渴速率: ×{data.thirstRateMult:F1}",
                $"环境暗度: {data.ambientDarkness * 100f:F0}%",
                $"日光强度: {data.sunIntensity:F1}  日光颜色: {data.sunColor}",
            };
            foreach (var lineTxt in lines)
            {
                var lbl = UguiMakeText("WData", 11, FontStyle.Normal, TextAnchor.MiddleLeft, 380, 18);
                lbl.text = lineTxt;
                lbl.transform.SetParent(_uguiWeatherScrollContent, false);
                _uguiWeatherRows.Add(lbl.gameObject);
            }
        }

        // ============================================================
        // UGUI 工具方法
        // ============================================================
        GameObject UguiMakeRow(GameObject parent, float h)
        {
            var go = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            go.transform.SetParent(parent.transform, false);
            var le = go.GetComponent<LayoutElement>();
            le.minHeight = h; le.preferredHeight = h;
            var rt = go.GetComponent<RectTransform>();
            var hlg = go.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = 3; hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = true;
            hlg.childControlWidth = false; hlg.childControlHeight = true;
            return go;
        }

        GameObject MakeInnerScrollRect(GameObject parent, float w, float h, out RectTransform contentRect)
        {
            var scrollGo = new GameObject("InnerScroll", typeof(RectTransform), typeof(ScrollRect), typeof(Image), typeof(RectMask2D));
            scrollGo.transform.SetParent(parent.transform, false);
            scrollGo.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f, 1f);
            var srt = scrollGo.GetComponent<RectTransform>();
            srt.sizeDelta = new Vector2(w, h);

            var vp = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            vp.transform.SetParent(scrollGo.transform, false);
            UguiSetStretch(vp.GetComponent<RectTransform>());
            vp.GetComponent<Image>().color = Color.clear;

            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(vp.transform, false);
            contentRect = contentGo.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1); contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.sizeDelta = new Vector2(0, 0);
            var vlg = contentGo.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 1; vlg.padding = new RectOffset(4, 4, 2, 2);
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            var csf = contentGo.GetComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.viewport = vp.GetComponent<RectTransform>();
            scroll.content = contentRect;
            scroll.horizontal = false;
            scroll.scrollSensitivity = 40f;
            return scrollGo;
        }

        Toggle UguiMakeToggle(string name, string label, bool initial)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Toggle), typeof(Image));
            go.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f, 1f);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(18, 18);

            var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(go.transform, false);
            UguiSetStretch(bg.GetComponent<RectTransform>());
            bg.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.15f, 1f);

            var checkmark = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
            checkmark.transform.SetParent(bg.transform, false);
            UguiSetStretch(checkmark.GetComponent<RectTransform>());
            checkmark.GetComponent<Image>().color = new Color(0f, 0.7f, 0f, 1f);

            var lblGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            lblGo.transform.SetParent(go.transform, false);
            UguiSetStretch(lblGo.GetComponent<RectTransform>());
            var txt = lblGo.GetComponent<Text>();
            txt.text = label;
            txt.font = _font;
            txt.fontSize = 12;
            txt.color = Color.white;
            txt.alignment = TextAnchor.MiddleLeft;
            txt.raycastTarget = false;

            var tog = go.GetComponent<Toggle>();
            tog.isOn = initial;
            tog.targetGraphic = bg.GetComponent<Image>();
            tog.graphic = checkmark.GetComponent<Image>();

            // 重新调整布局：背景和文字横向排列
            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 6;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;
            bg.GetComponent<RectTransform>().sizeDelta = new Vector2(16, 16);
            lblGo.GetComponent<RectTransform>().sizeDelta = new Vector2(360, 26);
            rt.sizeDelta = new Vector2(400, 26);

            return tog;
        }

        Slider UguiMakeSlider(string name, float min, float max, float initial)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Slider));
            var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(go.transform, false);
            UguiSetStretch(bg.GetComponent<RectTransform>());
            bg.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.15f, 1f);

            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(bg.transform, false);
            var frt = fill.GetComponent<RectTransform>();
            frt.anchorMin = Vector2.zero; frt.anchorMax = new Vector2(1, 1);
            frt.sizeDelta = new Vector2(0, 0);
            fill.GetComponent<Image>().color = new Color(0.3f, 0.5f, 0.3f, 1f);

            var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handle.transform.SetParent(go.transform, false);
            handle.GetComponent<Image>().color = new Color(0.6f, 0.6f, 0.6f, 1f);
            handle.GetComponent<RectTransform>().sizeDelta = new Vector2(10, 16);

            var slider = go.GetComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = initial;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.fillRect = fill.GetComponent<RectTransform>();
            slider.handleRect = handle.GetComponent<RectTransform>();
            slider.direction = Slider.Direction.LeftToRight;

            var srt = go.GetComponent<RectTransform>();
            srt.sizeDelta = new Vector2(200, 20);
            srt.anchorMin = new Vector2(0, 0.5f); srt.anchorMax = new Vector2(1, 0.5f);

            return slider;
        }

        Text UguiMakeInputText(GameObject parent)
        {
            var tgo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            tgo.transform.SetParent(parent.transform, false);
            UguiSetStretch(tgo.GetComponent<RectTransform>());
            var t = tgo.GetComponent<Text>();
            t.font = _font; t.fontSize = 12; t.color = Color.white;
            t.alignment = TextAnchor.MiddleLeft;
            t.supportRichText = false;
            return t;
        }

        static void UguiSetStretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.sizeDelta = Vector2.zero; }

        static void UguiAttach(GameObject go, GameObject parent, float x, float y, float anchorX, float anchorY)
        {
            go.transform.SetParent(parent.transform, false);
            var r = go.GetComponent<RectTransform>();
            if (r == null) r = go.AddComponent<RectTransform>();
            r.anchorMin = r.anchorMax = new Vector2(anchorX, anchorY);
            r.pivot = new Vector2(anchorX, anchorY);
            r.anchoredPosition = new Vector2(x, y);
        }

        static void UguiAttach(Component c, GameObject parent, float x, float y, float anchorX, float anchorY)
            => UguiAttach(c.gameObject, parent, x, y, anchorX, anchorY);

        Text UguiMakeText(string name, int size, FontStyle style, TextAnchor align, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(w, h);
            var t = go.GetComponent<Text>();
            t.font = _font;
            t.fontSize = size;
            t.fontStyle = style;
            t.alignment = align;
            t.color = new Color(0.9f, 0.9f, 0.9f);
            t.raycastTarget = false;
            return t;
        }

        Button UguiMakeSmallBtn(string name, string text, Color bg, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.GetComponent<Image>().color = bg;
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(w, h);

            var lblGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            lblGo.transform.SetParent(go.transform, false);
            UguiSetStretch(lblGo.GetComponent<RectTransform>());
            var lbl = lblGo.GetComponent<Text>();
            lbl.text = text; lbl.font = _font; lbl.fontSize = 11;
            lbl.alignment = TextAnchor.MiddleCenter; lbl.color = Color.white;
            lbl.raycastTarget = false;
            return go.GetComponent<Button>();
        }

        Button UguiMakeBigBtn(string name, string text, float w, float h, out Text label)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.GetComponent<Image>().color = new Color(0.25f, 0.4f, 0.25f, 1f);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(w, h);

            var lblGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            lblGo.transform.SetParent(go.transform, false);
            UguiSetStretch(lblGo.GetComponent<RectTransform>());
            label = lblGo.GetComponent<Text>();
            label.text = text; label.font = _font; label.fontSize = 12;
            label.alignment = TextAnchor.MiddleCenter; label.color = Color.white;
            label.raycastTarget = false;
            return go.GetComponent<Button>();
        }

    }
}
