using System.Collections.Generic;
using UnityEngine;
using _Game.Config;
using _Game.Core;

namespace _Game.Systems.Crafting
{
    /// <summary>
    /// 研究中心科技树面板 v2.0。
    /// 大类可展开子项列表，子项需大类先完成。
    /// </summary>
    public class ChemicalResearchUI : MonoBehaviour
    {
        [Header("布局")]
        public float panelX = 20f, panelY = 80f;
        public float panelWidth = 520f;
        public float rowHeight = 34f;
        public float childIndent = 24f;
        public float padding = 10f;

        bool _isVisible;
        ChemicalResearchManager _manager;
        Inventory.Inventory _inventory;
        Vector2 _scroll;
        ResearchTier _activeTier = ResearchTier.Early;
        HashSet<string> _expandedCategories = new HashSet<string>(); // 已展开的大类ID

        GUIStyle _headerStyle, _itemStyle, _btnStyle, _doneStyle, _descStyle, _costStyle, _closeBtnStyle;
        GUIStyle _tabStyle, _tabActiveStyle, _catStyle, _childItemStyle;
        bool _stylesReady;

        static readonly ResearchTier[] AllTiers = { ResearchTier.Early, ResearchTier.Mid, ResearchTier.Late, ResearchTier.Endgame };
        static readonly string[] TierNames = { "前期", "中期", "后期", "终局" };
        static readonly Color[] TierColors =
        {
            new Color(0.5f, 0.8f, 0.5f), new Color(0.5f, 0.6f, 1f),
            new Color(0.9f, 0.6f, 0.3f), new Color(1f, 0.35f, 0.35f),
        };

        static ChemicalResearchUI _instance;

        void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(this); return; }
            _instance = this;
            _manager = GetComponent<ChemicalResearchManager>();
            _inventory = GetComponent<Inventory.Inventory>();
        }

        void OnDestroy() { if (_instance == this) _instance = null; }
        void OnEnable() { EventBus.Subscribe<ResearchStationOpenedEvent>(OnResearchOpened); InputRouter.BindKey(KeyCode.Escape, InputPriority.UI, HandleEsc, this); }
        void OnDisable() { EventBus.Unsubscribe<ResearchStationOpenedEvent>(OnResearchOpened); InputRouter.UnbindAll(this); }
        public void Close() { _isVisible = false; }
        bool HandleEsc() { if (!_isVisible) return false; _isVisible = false; return true; }
        void OnResearchOpened(ResearchStationOpenedEvent evt) { _isVisible = true; }

        void OnGUI()
        {
            if (UIModeConfig.UseUGUI) return;
            if (!_isVisible || _manager == null || _manager.Data == null) return;
            InitStyles();

            var allProjects = _manager.Data.projects;
            if (allProjects == null || allProjects.Length == 0) return;

            // 构建显示列表：大类 + 已展开大类的子项
            var displayList = BuildDisplayList(allProjects);

            float totalH = displayList.Count * (rowHeight + 6f) + padding * 2 + 120f;
            float panelH = Mathf.Min(totalH, Screen.height - panelY - 40f);

            Rect bg = new Rect(panelX, panelY, panelWidth, panelH);
            GUI.color = new Color(0.06f, 0.06f, 0.08f, 0.95f);
            GUI.DrawTexture(bg, Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUILayout.BeginArea(new Rect(bg.x + padding, bg.y + padding, bg.width - padding * 2, bg.height - padding * 2));

            // 标题
            GUILayout.BeginHorizontal();
            GUILayout.Label("研究中心", _headerStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("✕", _closeBtnStyle, GUILayout.Width(30f), GUILayout.Height(24f)))
                _isVisible = false;
            GUILayout.EndHorizontal();
            GUILayout.Space(4f);

            // Tab
            GUILayout.BeginHorizontal();
            for (int i = 0; i < AllTiers.Length; i++)
            {
                var tier = AllTiers[i];
                int count = 0;
                foreach (var p in allProjects) if (p.tier == tier && p.isCategory) count++;
                bool active = _activeTier == tier;
                GUI.backgroundColor = active ? TierColors[i] : new Color(0.2f, 0.2f, 0.2f, 1f);
                if (GUILayout.Button($"{TierNames[i]} ({count})", active ? _tabActiveStyle : _tabStyle, GUILayout.Height(26f)))
                { _activeTier = tier; _scroll = Vector2.zero; }
            }
            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();
            GUILayout.Space(6f);

            // 列表
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));

            foreach (var entry in displayList)
            {
                DrawProjectRow(entry);
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        // 显示条目
        struct DisplayEntry
        {
            public ChemicalResearchProject project;
            public bool isChild;
            public string parentId;
        }

        List<DisplayEntry> BuildDisplayList(ChemicalResearchProject[] all)
        {
            var list = new List<DisplayEntry>();
            foreach (var p in all)
            {
                if (p.tier != _activeTier) continue;

                if (p.isCategory)
                {
                    // 大类始终显示
                    list.Add(new DisplayEntry { project = p, isChild = false });

                    // 如果已展开，插入子项
                    if (_expandedCategories.Contains(p.researchId))
                    {
                        var children = _manager.GetChildProjects(p.researchId);
                        foreach (var c in children)
                            list.Add(new DisplayEntry { project = c, isChild = true, parentId = p.researchId });
                    }
                }
                else if (string.IsNullOrEmpty(p.parentResearchId))
                {
                    // 无父类的独立项目（兼容旧数据）
                    list.Add(new DisplayEntry { project = p, isChild = false });
                }
            }
            return list;
        }

        void DrawProjectRow(DisplayEntry entry)
        {
            var proj = entry.project;
            bool isCategory = proj.isCategory;
            bool done = _manager.IsResearched(proj.researchId);
            bool categoryDone = true;
            if (entry.isChild)
                categoryDone = _manager.IsCategoryResearched(entry.parentId);

            bool canDo = _manager.CanResearch(proj.researchId, _inventory);
            // 子项：大类未完成则不可研究
            if (entry.isChild && !categoryDone) canDo = false;

            float indent = entry.isChild ? childIndent : 0f;
            Color rowBg = isCategory ? new Color(0.12f, 0.14f, 0.12f, 0.5f) : new Color(0.08f, 0.08f, 0.08f, 0.3f);

            GUILayout.BeginHorizontal(GUI.skin.box, GUILayout.Height(rowHeight + 4f));

            // 缩进
            if (indent > 0) GUILayout.Space(indent);

            // 展开/折叠按钮（大类）
            if (isCategory)
            {
                bool expanded = _expandedCategories.Contains(proj.researchId);
                string arrow = expanded ? "▼" : "▶";
                if (GUILayout.Button(arrow, _btnStyle, GUILayout.Width(22f), GUILayout.Height(rowHeight - 4f)))
                {
                    if (expanded) _expandedCategories.Remove(proj.researchId);
                    else _expandedCategories.Add(proj.researchId);
                }
            }
            else if (entry.isChild)
            {
                GUILayout.Space(4f);
            }

            // 状态图标
            string statusIcon;
            GUIStyle statusStyle;
            if (done) { statusIcon = "✓"; statusStyle = _doneStyle; }
            else if (!categoryDone && entry.isChild) { statusIcon = "🔒"; statusStyle = _costStyle; }
            else if (canDo) { statusIcon = "→"; statusStyle = _btnStyle; }
            else { statusIcon = "✗"; statusStyle = _costStyle; }
            GUILayout.Label(statusIcon, statusStyle, GUILayout.Width(24f));

            // 名称 + 描述
            GUILayout.BeginVertical();
            var nameStyle = isCategory ? _catStyle : _itemStyle;
            GUILayout.Label(proj.displayName, nameStyle);
            string desc = proj.description;
            if (proj.unlockedDeviceNames != null && proj.unlockedDeviceNames.Length > 0)
                desc += "  [" + string.Join("、", proj.unlockedDeviceNames) + "]";
            if (proj.unlockedRecipeIds != null && proj.unlockedRecipeIds.Length > 0)
                desc += "  配方:" + string.Join("、", proj.unlockedRecipeIds);
            GUILayout.Label(desc, _descStyle);
            GUILayout.EndVertical();

            GUILayout.FlexibleSpace();

            // 费用
            string costStr = "";
            if (proj.cost != null)
            {
                var parts = new List<string>();
                foreach (var c in proj.cost) parts.Add($"{ItemName(c.itemData)}×{c.count}");
                costStr = string.Join(" ", parts);
            }
            GUILayout.Label(costStr, _costStyle, GUILayout.Width(160f));

            // 智力标签
            GUILayout.Label($"智{proj.requiredIntellectLevel}", _descStyle, GUILayout.Width(28f));

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

        void InitStyles()
        {
            if (_stylesReady) return;
            _stylesReady = true;

            _headerStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.6f, 1f, 0.6f) } };
            _itemStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            _catStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.8f, 1f, 0.5f) } };
            _descStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, normal = { textColor = new Color(0.6f, 0.6f, 0.6f) } };
            _costStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, normal = { textColor = new Color(0.8f, 0.7f, 0.4f) } };
            _btnStyle = new GUIStyle(GUI.skin.button) { fontSize = 12, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.5f, 1f, 0.5f) } };
            _closeBtnStyle = new GUIStyle(GUI.skin.button) { fontSize = 14, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.9f, 0.4f, 0.4f) }, hover = { textColor = new Color(1f, 0.3f, 0.3f) } };
            _doneStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.3f, 0.8f, 0.3f) } };
            _tabStyle = new GUIStyle(GUI.skin.button) { fontSize = 13, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.7f, 0.7f, 0.7f) }, padding = new RectOffset(10, 10, 4, 4) };
            _tabActiveStyle = new GUIStyle(GUI.skin.button) { fontSize = 13, fontStyle = FontStyle.Bold, normal = { textColor = Color.white }, padding = new RectOffset(10, 10, 4, 4) };
        }

        static string ItemName(ItemData item) => item != null ? item.itemName : "???";
    }
}
