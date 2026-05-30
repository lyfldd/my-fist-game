using System.Collections.Generic;
using UnityEngine;
using _Game.Config;
using _Game.Core;

namespace _Game.Systems.Crafting
{
    /// <summary>
    /// 研究中心面板。四层递进：前期/中期/后期/终局。
    /// ResearchStationOpenedEvent 时显示。
    /// </summary>
    public class ChemicalResearchUI : MonoBehaviour
    {
        [Header("布局")]
        public float panelX = 20f, panelY = 80f;
        public float panelWidth = 500f;
        public float rowHeight = 36f;
        public float padding = 10f;

        bool _isVisible;
        ChemicalResearchManager _manager;
        Inventory.Inventory _inventory;
        Vector2 _scroll;
        ResearchTier _activeTier = ResearchTier.Early;
        GUIStyle _headerStyle, _itemStyle, _btnStyle, _doneStyle, _descStyle, _costStyle, _closeBtnStyle;
        GUIStyle _tabStyle, _tabActiveStyle;
        bool _stylesReady;

        static readonly ResearchTier[] AllTiers = { ResearchTier.Early, ResearchTier.Mid, ResearchTier.Late, ResearchTier.Endgame };
        static readonly string[] TierNames = { "前期", "中期", "后期", "终局" };
        static readonly Color[] TierColors =
        {
            new Color(0.5f, 0.8f, 0.5f),  // 前期 绿
            new Color(0.5f, 0.6f, 1f),    // 中期 蓝
            new Color(0.9f, 0.6f, 0.3f),  // 后期 橙
            new Color(1f, 0.35f, 0.35f),  // 终局 红
        };

        static ChemicalResearchUI _instance;

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning($"[ChemicalResearchUI] 检测到重复实例，销毁 {gameObject.name} 上的副本");
                Destroy(this);
                return;
            }
            _instance = this;

            _manager = GetComponent<ChemicalResearchManager>();
            _inventory = GetComponent<Inventory.Inventory>();
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        void OnEnable()
        {
            EventBus.Subscribe<ResearchStationOpenedEvent>(OnResearchOpened);
            InputRouter.BindKey(KeyCode.Escape, InputPriority.UI, HandleEsc, this);
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<ResearchStationOpenedEvent>(OnResearchOpened);
            InputRouter.UnbindAll(this);
        }

        public void Close()
        {
            _isVisible = false;
        }

        bool HandleEsc()
        {
            if (!_isVisible) return false;
            _isVisible = false;
            return true;
        }

        void OnResearchOpened(ResearchStationOpenedEvent evt)
        {
            _isVisible = true;
        }

        void OnGUI()
        {
            if (!_isVisible || _manager == null || _manager.Data == null) return;
            InitStyles();

            var allProjects = _manager.Data.projects;
            if (allProjects == null || allProjects.Length == 0) return;

            // 筛选当前 Tab 的项目
            var projects = new List<ChemicalResearchProject>();
            foreach (var p in allProjects)
                if (p.tier == _activeTier) projects.Add(p);

            // 动态高度
            float headerH = 70f;
            float tabBarH = 34f;
            float contentH = projects.Count * (rowHeight + 6f) + padding * 2;
            float totalH = headerH + tabBarH + Mathf.Min(contentH, 400f) + padding;
            float panelH = Mathf.Min(totalH, Screen.height - panelY - 40f);

            Rect bg = new Rect(panelX, panelY, panelWidth, panelH);

            GUI.color = new Color(0.06f, 0.06f, 0.08f, 0.95f);
            GUI.DrawTexture(bg, Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUILayout.BeginArea(new Rect(bg.x + padding, bg.y + padding, bg.width - padding * 2, bg.height - padding * 2));

            // 标题栏
            GUILayout.BeginHorizontal();
            GUILayout.Label("研究中心", _headerStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("✕", _closeBtnStyle, GUILayout.Width(30f), GUILayout.Height(24f)))
                _isVisible = false;
            GUILayout.EndHorizontal();
            GUILayout.Space(4f);

            // Tab 栏
            GUILayout.BeginHorizontal();
            for (int i = 0; i < AllTiers.Length; i++)
            {
                var tier = AllTiers[i];
                int count = 0;
                foreach (var p in allProjects) if (p.tier == tier) count++;

                bool active = _activeTier == tier;
                var style = active ? _tabActiveStyle : _tabStyle;
                GUI.backgroundColor = active ? TierColors[i] : new Color(0.2f, 0.2f, 0.2f, 1f);
                if (GUILayout.Button($"{TierNames[i]} ({count})", style, GUILayout.Height(26f)))
                {
                    _activeTier = tier;
                    _scroll = Vector2.zero;
                }
            }
            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();
            GUILayout.Space(4f);

            // 项目列表
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));

            if (projects.Count == 0)
            {
                GUILayout.Label("该阶段暂无研究项目", _descStyle);
            }

            foreach (var proj in projects)
            {
                bool done = _manager.IsResearched(proj.researchId);
                bool canDo = _manager.CanResearch(proj.researchId, _inventory);

                GUILayout.BeginHorizontal(GUI.skin.box, GUILayout.Height(rowHeight + 8f));

                // 状态图标
                var statusStyle = done ? _doneStyle : (canDo ? _btnStyle : _costStyle);
                string statusIcon = done ? "✓" : (canDo ? "→" : "✗");
                GUILayout.Label(statusIcon, statusStyle, GUILayout.Width(28f));

                // 名称 + 描述
                GUILayout.BeginVertical();
                GUILayout.Label(proj.displayName, _itemStyle);
                string desc = proj.description;
                if (proj.unlockedDeviceNames != null && proj.unlockedDeviceNames.Length > 0)
                    desc += "  解锁设备: " + string.Join("、", proj.unlockedDeviceNames);
                if (proj.unlockedRecipeIds != null && proj.unlockedRecipeIds.Length > 0)
                    desc += "  解锁配方: " + string.Join("、", proj.unlockedRecipeIds);
                GUILayout.Label(desc, _descStyle);
                GUILayout.EndVertical();

                GUILayout.FlexibleSpace();

                // 费用
                string costStr = "";
                if (proj.cost != null)
                {
                    var parts = new List<string>();
                    foreach (var c in proj.cost)
                        parts.Add($"{ItemName(c.itemData)}×{c.count}");
                    costStr = string.Join("  ", parts);
                }
                GUILayout.Label(costStr, _costStyle, GUILayout.Width(150f));

                // 智力要求
                if (proj.requiredIntellectLevel > 0)
                {
                    GUILayout.Label($"智{proj.requiredIntellectLevel}", _descStyle, GUILayout.Width(32f));
                }

                // 研究按钮
                if (!done)
                {
                    GUI.enabled = canDo;
                    if (GUILayout.Button("研究", GUILayout.Width(50f), GUILayout.Height(rowHeight - 4f)))
                        _manager.TryResearch(proj.researchId, _inventory);
                    GUI.enabled = true;
                }
                else
                {
                    GUILayout.Label("已研究", _doneStyle, GUILayout.Width(50f));
                }

                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void InitStyles()
        {
            if (_stylesReady) return;
            _stylesReady = true;

            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18, fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.6f, 1f, 0.6f) }
            };
            _itemStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14, fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            _descStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                normal = { textColor = new Color(0.6f, 0.6f, 0.6f) }
            };
            _costStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                normal = { textColor = new Color(0.8f, 0.7f, 0.4f) }
            };
            _btnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12, fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.5f, 1f, 0.5f) }
            };
            _closeBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14, fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.9f, 0.4f, 0.4f) },
                hover = { textColor = new Color(1f, 0.3f, 0.3f) }
            };
            _doneStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16, fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.3f, 0.8f, 0.3f) }
            };
            _tabStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13, fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.7f, 0.7f, 0.7f) },
                padding = new RectOffset(10, 10, 4, 4)
            };
            _tabActiveStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13, fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white },
                padding = new RectOffset(10, 10, 4, 4)
            };
        }

        static string ItemName(ItemData item) => item != null ? item.itemName : "???";
    }
}
