using System.Collections.Generic;
using UnityEngine;
using _Game.Config;
using _Game.Core;

namespace _Game.Systems.Crafting
{
    /// <summary>
    /// 研究中心面板。ResearchStationOpenedEvent 时显示研究项目列表。
    /// </summary>
    public class ChemicalResearchUI : MonoBehaviour
    {
        [Header("布局")]
        public float panelX = 20f, panelY = 80f;
        public float panelWidth = 380f;
        public float rowHeight = 32f;
        public float padding = 8f;

        bool _isVisible;
        ChemicalResearchManager _manager;
        Inventory.Inventory _inventory;
        Vector2 _scroll;
        GUIStyle _headerStyle, _itemStyle, _btnStyle, _doneStyle, _descStyle, _costStyle, _closeBtnStyle;
        bool _stylesReady;

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

            var projects = _manager.Data.projects;
            if (projects == null || projects.Length == 0) return;

            float height = projects.Length * (rowHeight + 4f) + padding * 2 + 30f;
            Rect bg = new Rect(panelX, panelY, panelWidth, Mathf.Min(height, Screen.height - panelY - 40f));

            GUI.color = new Color(0.06f, 0.06f, 0.08f, 0.93f);
            GUI.DrawTexture(bg, Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUILayout.BeginArea(new Rect(bg.x + padding, bg.y + 4f, bg.width - padding * 2, bg.height - 8f));

            // 标题栏
            GUILayout.Label("研究中心", _headerStyle);
            GUILayout.Space(4f);

            // 关闭按钮（固定坐标，避免 GUILayout 嵌套问题）
            Rect closeRect = new Rect(bg.width - padding * 2 - 28f, 4f, 28f, 24f);
            if (GUI.Button(closeRect, "✕", _closeBtnStyle ?? GUI.skin.button))
                _isVisible = false;

            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(bg.height - 30f));

            foreach (var proj in projects)
            {
                bool done = _manager.IsResearched(proj.researchId);
                bool canDo = _manager.CanResearch(proj.researchId, _inventory);

                GUILayout.BeginHorizontal(GUI.skin.box, GUILayout.Height(rowHeight + 10f));

                // 状态图标
                var statusStyle = done ? _doneStyle : (canDo ? _btnStyle : _costStyle);
                string statusIcon = done ? "✓" : (canDo ? "→" : "✗");
                GUILayout.Label(statusIcon, statusStyle, GUILayout.Width(24f));

                // 名称 + 描述
                GUILayout.BeginVertical();
                GUILayout.Label(proj.displayName, _itemStyle);
                GUILayout.Label(proj.description, _descStyle);
                GUILayout.EndVertical();

                GUILayout.FlexibleSpace();

                // 费用
                string costStr = "";
                if (proj.cost != null)
                {
                    var parts = new List<string>();
                    foreach (var c in proj.cost)
                        parts.Add($"{ItemName(c.itemData)}×{c.count}");
                    costStr = string.Join(" ", parts);
                }
                GUILayout.Label(costStr, _costStyle, GUILayout.Width(140f));

                // 研究按钮
                if (!done)
                {
                    GUI.enabled = canDo;
                    if (GUILayout.Button("研究", GUILayout.Width(50f), GUILayout.Height(rowHeight - 4f)))
                        _manager.TryResearch(proj.researchId, _inventory);
                    GUI.enabled = true;
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
        }

        static string ItemName(ItemData item) => item != null ? item.itemName : "???";
    }
}
