using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using _Game.UI;

namespace _Game.Editor
{
    /// <summary>
    /// 一键预构建所有 UI 结构到场景中。
    /// 运行后各 UI 脚本不再需要运行时 new GameObject，直接 Find 已有对象。
    ///
    /// 用法：菜单 Tools → Preconfigure All UI
    /// </summary>
    public static class PreconfigureUI
    {
        [MenuItem("Tools/Clean Orphan UI")]
        public static void CleanOrphanUI()
        {
            int cleaned = CleanupStandaloneUI();
            Debug.Log($"[CleanOrphanUI] 清理完成: {cleaned} 个孤立 UI 对象");
        }

        [MenuItem("Tools/Preconfigure All UI")]
        public static void RunAll()
        {
            var player = GameObject.FindWithTag("Player");
            if (player == null) { Debug.LogError("[PreconfigureUI] ❌ 场景中没有 tag=Player！"); return; }

            // 清理老式独立 UI GameObject
            int cleaned = CleanupStandaloneUI();

            int created = 0;
            created += BuildCrosshair(player);
            created += BuildDecibelHUD(player);
            created += BuildWeatherHUD(player);
            created += BuildTopLeftHUD(player);
            created += BuildSurvivalHUD(player);
            created += BuildQuickItemBar(player);
            created += BuildInventoryPanels(player);
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            EditorUtility.SetDirty(player);
            Debug.Log($"[PreconfigureUI] ✅ 完成！创建了 {created} 个 UI 对象到 Player");
        }

        static int BuildCrosshair(GameObject player)
        {
            if (FindChild(player, "CrosshairCanvas")) return 0;
            var canvas = CreateChildCanvas(player, "CrosshairCanvas", 999);
            var cross = new GameObject("Crosshair", typeof(RectTransform), typeof(Image));
            cross.transform.SetParent(canvas.transform, false);
            var img = cross.GetComponent<Image>();
            var tex = CreateCrosshairTexture(32);
            img.sprite = Sprite.Create(tex, new Rect(0, 0, 32, 32), Vector2.one * 0.5f);
            img.preserveAspect = true;
            img.raycastTarget = false;
            cross.GetComponent<RectTransform>().sizeDelta = new Vector2(24, 24);
            return 1;
        }

        static int BuildDecibelHUD(GameObject player)
        {
            if (FindChild(player, "DecibelHUD_Canvas")) return 0;
            var canvas = CreateChildCanvas(player, "DecibelHUD_Canvas", 50);
            CreateText(canvas.transform, "NoiseText", 300, 24, 16, TextAnchor.UpperLeft,
                new Vector2(0, 1), new Vector2(10, -45));
            return 1;
        }

        static int BuildWeatherHUD(GameObject player)
        {
            if (FindChild(player, "WeatherHUD_Canvas")) return 0;
            var canvas = CreateChildCanvas(player, "WeatherHUD_Canvas", 50);
            CreateText(canvas.transform, "WeatherLabel", 300, 22, 15, TextAnchor.UpperLeft,
                new Vector2(0, 1), new Vector2(10, -75));
            CreateText(canvas.transform, "TempLabel", 300, 22, 15, TextAnchor.UpperLeft,
                new Vector2(0, 1), new Vector2(10, -95));
            return 1;
        }

        static int BuildTopLeftHUD(GameObject player)
        {
            if (FindChild(player, "TopLeftHUD_Canvas")) return 0;
            var canvas = CreateChildCanvas(player, "TopLeftHUD_Canvas", 50);
            var main = CreateText(canvas.transform, "MainText", 350, 60, 18, TextAnchor.UpperLeft,
                new Vector2(0, 1), new Vector2(10, -10));
            main.supportRichText = true;
            var wpn = CreateText(canvas.transform, "WeaponText", 350, 24, 15, TextAnchor.UpperLeft,
                new Vector2(0, 1), new Vector2(10, -110));
            wpn.supportRichText = true;
            var hp = CreateText(canvas.transform, "HPText", 350, 24, 15, TextAnchor.UpperLeft,
                new Vector2(0, 1), new Vector2(10, -132));
            hp.supportRichText = true;
            return 1;
        }

        static int BuildSurvivalHUD(GameObject player)
        {
            if (FindChild(player, "SurvivalHUD_Canvas")) return 0;
            var canvas = CreateChildCanvas(player, "SurvivalHUD_Canvas", 100);

            float barW = 200f, barH = 20f, px = 20f, py = 20f;
            Color bg = new Color(0.15f, 0.15f, 0.15f, 0.85f);

            CreateStatBar(canvas.transform, "HealthBar", "生命", barW, barH, -px, -py, Color.red, bg, out _, out _);
            py += barH + 6;
            CreateStatBar(canvas.transform, "HungerBar", "饥饿", barW, barH, -px, -py, new Color(0.8f, 0.6f, 0f), bg, out _, out _);
            py += barH + 6;
            CreateStatBar(canvas.transform, "ThirstBar", "口渴", barW, barH, -px, -py, new Color(0f, 0.5f, 1f), bg, out _, out _);
            py += barH + 6;
            CreateStatBar(canvas.transform, "TempBar", "体温", barW, barH, -px, -py, new Color(1f, 0.5f, 0f), bg, out _, out _);
            return 1;
        }

        static int BuildQuickItemBar(GameObject player)
        {
            if (FindChild(player, "QuickItemBar_Canvas")) return 0;
            var canvas = CreateChildCanvas(player, "QuickItemBar_Canvas", 90);
            var gr = canvas.GetComponent<GraphicRaycaster>();
            if (gr != null) gr.blockingObjects = GraphicRaycaster.BlockingObjects.None;

            // SlotGrid
            int maxSlots = 10, slotSize = 50, spacing = 3;
            float totalWidth = maxSlots * slotSize + (maxSlots - 1) * spacing;
            var gridGo = new GameObject("SlotGrid", typeof(RectTransform));
            gridGo.transform.SetParent(canvas.transform, false);
            var gridRt = gridGo.GetComponent<RectTransform>();
            gridRt.anchorMin = gridRt.anchorMax = gridRt.pivot = new Vector2(0.5f, 0);
            gridRt.sizeDelta = new Vector2(totalWidth, slotSize);
            gridRt.anchoredPosition = new Vector2(0, 20);
            var glg = gridGo.AddComponent<GridLayoutGroup>();
            glg.cellSize = new Vector2(slotSize, slotSize);
            glg.spacing = new Vector2(spacing, 0);
            glg.childAlignment = TextAnchor.MiddleCenter;
            glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            glg.constraintCount = maxSlots;

            for (int i = 0; i < maxSlots; i++)
            {
                var slot = new GameObject($"Slot_{i}", typeof(Image), typeof(RectTransform));
                slot.transform.SetParent(gridGo.transform, false);
                slot.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

                CreateSlotText(slot.transform, "KeyNum", 18, 16, 11, TextAnchor.UpperLeft,
                    new Vector2(0, 1), new Vector2(2, -2), new Color(1, 1, 1, 0.5f), GetKeyLabel(i));
                CreateSlotText(slot.transform, "ItemName", slotSize - 4, 16, 9, TextAnchor.UpperCenter,
                    new Vector2(0.5f, 1), new Vector2(slotSize / 2f, -2), Color.white, "");
                CreateSlotText(slot.transform, "Count", slotSize - 4, 14, 11, TextAnchor.LowerCenter,
                    new Vector2(0.5f, 0), new Vector2(slotSize / 2f, 2), Color.yellow, "");

                // 耐久条（底部 3px，预建结构，运行时 RefreshAll 控制显隐）
                var durBar = new GameObject("DurBar", typeof(RectTransform));
                durBar.transform.SetParent(slot.transform, false);
                var drt = durBar.GetComponent<RectTransform>();
                drt.anchorMin = new Vector2(0, 0); drt.anchorMax = new Vector2(1, 0);
                drt.pivot = new Vector2(0.5f, 0);
                drt.anchoredPosition = new Vector2(0, 2); drt.sizeDelta = new Vector2(-4, 3);
                var durBg = new GameObject("DurBG", typeof(RectTransform), typeof(Image));
                durBg.transform.SetParent(durBar.transform, false);
                durBg.GetComponent<Image>().color = new Color(0, 0, 0, 0.5f);
                var bgRt = durBg.GetComponent<RectTransform>();
                bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
                bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;
                var durFill = new GameObject("DurFill", typeof(RectTransform), typeof(Image));
                durFill.transform.SetParent(durBar.transform, false);
                durFill.GetComponent<Image>().color = Color.green;
                var fillRt = durFill.GetComponent<RectTransform>();
                fillRt.anchorMin = Vector2.zero; fillRt.anchorMax = Vector2.one;
                fillRt.pivot = new Vector2(0, 0.5f);
                fillRt.offsetMin = Vector2.zero; fillRt.offsetMax = Vector2.zero;
                durBar.SetActive(false);

                CreateBorder(slot.transform, slotSize);
            }

            // Arrows
            CreateArrowText(canvas.transform, "ArrowLeft", "<", new Vector2(1, 0.5f),
                new Vector2(-totalWidth / 2 - 8, slotSize / 2 + 20));
            CreateArrowText(canvas.transform, "ArrowRight", ">", new Vector2(0, 0.5f),
                new Vector2(totalWidth / 2 + 8, slotSize / 2 + 20));
            return 1;
        }

        static int BuildInventoryPanels(GameObject player)
        {
            var invUI = player.GetComponent<_Game.UI.InventoryUI>();
            if (invUI == null)
            {
                Debug.LogWarning("[PreconfigureUI] Player 上没有 InventoryUI 组件，跳过背包面板创建");
                return 0;
            }

            // 找或建 InventoryCanvas
            var canvasT = player.transform.Find("InventoryCanvas");
            Canvas canvas;
            if (canvasT != null)
                canvas = canvasT.GetComponent<Canvas>();
            else
                canvas = CreateChildCanvas(player, "InventoryCanvas", 0);

            int created = 0;

            // 总览面板（Tab）
            if (invUI.overviewPanel == null && canvas.transform.Find("OverviewPanel") == null)
            {
                var panel = new GameObject("OverviewPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                panel.transform.SetParent(canvas.transform, false);
                StretchRT(panel.GetComponent<RectTransform>());
                panel.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.1f, 0.95f);

                // TopTabBar
                CreateTopTabBar(panel.transform, 700);

                // GridContainer: 动态网格内容（每次刷新重建）
                var grid = new GameObject("GridContainer", typeof(RectTransform));
                grid.transform.SetParent(panel.transform, false);
                var grt = grid.GetComponent<RectTransform>();
                grt.anchorMin = Vector2.zero; grt.anchorMax = Vector2.one;
                grt.offsetMin = new Vector2(10, 10); grt.offsetMax = new Vector2(-10, -10);

                invUI.overviewPanel = panel;
                invUI.overviewGridContainer = grid;

                // info text
                var infoGo = CreateText(panel.transform, "OverviewInfoText", 600, 30, 16,
                    TextAnchor.UpperCenter, new Vector2(0.5f, 1), new Vector2(0, -5));
                invUI.overviewInfoText = infoGo;

                EditorUtility.SetDirty(invUI);
                created++;
                Debug.Log("[PreconfigureUI] OverviewPanel 已创建");
            }

            // 快捷面板（V）
            if (invUI.quickPanel == null && canvas.transform.Find("QuickPanel") == null)
            {
                var qpanel = new GameObject("QuickPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                qpanel.transform.SetParent(canvas.transform, false);
                StretchRT(qpanel.GetComponent<RectTransform>());
                qpanel.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.08f, 0.9f);

                var qgrid = new GameObject("GridContainer", typeof(RectTransform));
                qgrid.transform.SetParent(qpanel.transform, false);
                var qgrt = qgrid.GetComponent<RectTransform>();
                qgrt.anchorMin = Vector2.zero; qgrt.anchorMax = Vector2.one;
                qgrt.offsetMin = new Vector2(10, 10); qgrt.offsetMax = new Vector2(-10, -10);

                invUI.quickPanel = qpanel;
                invUI.quickGridContainer = qgrid;

                created++;
                Debug.Log("[PreconfigureUI] QuickPanel 已创建");
            }

            // StaticShell（每次重建，不依赖 overviewPanel 首次创建）
            var overviewPanel = invUI.overviewPanel;
            if (overviewPanel != null)
            {
                var oldShell = overviewPanel.transform.Find("StaticShell");
                if (oldShell != null) Object.DestroyImmediate(oldShell.gameObject);
                var shell = new GameObject("StaticShell", typeof(RectTransform));
                shell.transform.SetParent(overviewPanel.transform, false);
                StretchRT(shell.GetComponent<RectTransform>());
                BuildStaticShell(shell.transform, 700);
                created++;
                Debug.Log("[PreconfigureUI] StaticShell 已创建");
            }

            // 隐藏面板（Start 时由代码控制显示）
            if (invUI.overviewPanel != null) invUI.overviewPanel.SetActive(false);
            if (invUI.quickPanel != null) invUI.quickPanel.SetActive(false);

            return created;
        }

        static void BuildStaticShell(Transform shellParent, float panelW)
        {
            float ph = 1f; // 占位——实际渲染时由 ScrollRect 控制
            float m = 3f, leftW = panelW * 0.28f, rightW = panelW * 0.72f;

            // 左面板背景
            var leftPanel = new GameObject("LeftPanel", typeof(RectTransform), typeof(Image));
            leftPanel.transform.SetParent(shellParent, false);
            var lpRt = leftPanel.GetComponent<RectTransform>();
            lpRt.anchorMin = new Vector2(0, 0); lpRt.anchorMax = new Vector2(0, 1);
            lpRt.pivot = new Vector2(0, 0);
            lpRt.anchoredPosition = new Vector2(m, 0);
            lpRt.sizeDelta = new Vector2(leftW - m, 0);
            leftPanel.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.08f, 0.6f);
            leftPanel.GetComponent<Image>().raycastTarget = false;
            // 隐藏整个 shell，ShowEquipTabContent 时显示
            shellParent.gameObject.SetActive(false);

            // 武器槽 2×2 标题
            var wpnTitleGo = new GameObject("WpnTitle", typeof(RectTransform), typeof(Text));
            wpnTitleGo.transform.SetParent(leftPanel.transform, false);
            var wt = wpnTitleGo.GetComponent<Text>();
            wt.text = "— 武器 —";
            wt.fontSize = 11;
            wt.color = new Color(0.5f, 0.6f, 0.5f);
            wt.alignment = TextAnchor.MiddleCenter;
            wt.font = UGUIBuilder.DefaultFont;
            var wtrt = wpnTitleGo.GetComponent<RectTransform>();
            wtrt.anchorMin = wtrt.anchorMax = wtrt.pivot = new Vector2(0, 1);
            wtrt.sizeDelta = new Vector2(leftW - m * 2, 18);

            // 武器槽 4 个格子 (2×2)
            float cellSize = Mathf.Min((leftW - 2f) / 2f, 40f);
            string[] wpnNames = { "主武", "副武", "小刀", "手枪" };
            for (int i = 0; i < 4; i++)
            {
                int row = i / 2, col = i % 2;
                var cell = new GameObject($"Wpn_{wpnNames[i]}", typeof(RectTransform), typeof(Image));
                cell.transform.SetParent(leftPanel.transform, false);
                var crt = cell.GetComponent<RectTransform>();
                crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(0, 1);
                crt.sizeDelta = new Vector2(cellSize, cellSize);
                crt.anchoredPosition = new Vector2(m + col * (cellSize + 2f), -(20 + row * (cellSize + 2f)));
                cell.GetComponent<Image>().color = new Color(0.06f, 0.06f, 0.08f, 0.6f);
                cell.GetComponent<Image>().raycastTarget = true;

                var lbl = new GameObject("Label", typeof(RectTransform), typeof(Text));
                lbl.transform.SetParent(cell.transform, false);
                var lt = lbl.GetComponent<Text>();
                lt.text = wpnNames[i];
                lt.fontSize = 9;
                lt.color = new Color(0.5f, 0.5f, 0.5f);
                lt.alignment = TextAnchor.MiddleCenter;
                lt.font = UGUIBuilder.DefaultFont;
                StretchRT(lbl.GetComponent<RectTransform>());
            }

            // 纸娃娃
            float dollTop = -(20 + 2 * (cellSize + 2f) + 8);
            float dollH = leftW * 0.7f;
            var dollBg = new GameObject("DollBg", typeof(RectTransform), typeof(Image));
            dollBg.transform.SetParent(leftPanel.transform, false);
            var drt = dollBg.GetComponent<RectTransform>();
            drt.anchorMin = drt.anchorMax = drt.pivot = new Vector2(0, 1);
            drt.sizeDelta = new Vector2(leftW * 0.8f, dollH);
            drt.anchoredPosition = new Vector2(leftW * 0.1f, dollTop);
            dollBg.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.15f, 0.6f);
            dollBg.GetComponent<Image>().raycastTarget = false;

            // 7 个装备槽
            string[] dollNames = { "头部", "胸挂", "上衣", "防弹衣", "腰带", "裤子", "背包" };
            float slotH = (dollH - 14f) / 7f;
            for (int i = 0; i < 7; i++)
            {
                var slot = new GameObject($"Doll_{dollNames[i]}", typeof(RectTransform), typeof(Image));
                slot.transform.SetParent(dollBg.transform, false);
                var srt = slot.GetComponent<RectTransform>();
                srt.anchorMin = srt.anchorMax = srt.pivot = new Vector2(0, 1);
                srt.sizeDelta = new Vector2(leftW * 0.8f - 4f, slotH - 2f);
                srt.anchoredPosition = new Vector2(2f, -(2f + i * slotH));
                slot.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.13f, 0.75f);
                slot.GetComponent<Image>().raycastTarget = true;

                var slbl = new GameObject("Label", typeof(RectTransform), typeof(Text));
                slbl.transform.SetParent(slot.transform, false);
                var slt = slbl.GetComponent<Text>();
                slt.text = $"{dollNames[i]}: 空";
                slt.fontSize = 10;
                slt.color = new Color(0.45f, 0.45f, 0.45f);
                slt.alignment = TextAnchor.MiddleCenter;
                slt.font = UGUIBuilder.DefaultFont;
                StretchRT(slbl.GetComponent<RectTransform>());
            }

            // 属性面板
            float statTop = dollTop - dollH - 12;
            var statBg = new GameObject("StatsPanel", typeof(RectTransform), typeof(Image));
            statBg.transform.SetParent(leftPanel.transform, false);
            var sbrt = statBg.GetComponent<RectTransform>();
            sbrt.anchorMin = sbrt.anchorMax = sbrt.pivot = new Vector2(0, 1);
            sbrt.sizeDelta = new Vector2(leftW - m * 2, 60);
            sbrt.anchoredPosition = new Vector2(m, statTop);
            statBg.GetComponent<Image>().color = new Color(0.06f, 0.06f, 0.09f, 0.5f);
            statBg.GetComponent<Image>().raycastTarget = false;

            string[] statNames = { "护甲", "保暖", "负重" };
            for (int i = 0; i < 3; i++)
            {
                var labelGo = new GameObject($"StatLbl_{statNames[i]}", typeof(RectTransform), typeof(Text));
                labelGo.transform.SetParent(statBg.transform, false);
                var lt = labelGo.GetComponent<Text>();
                lt.text = statNames[i];
                lt.fontSize = 9;
                lt.color = new Color(0.6f, 0.6f, 0.6f);
                lt.font = UGUIBuilder.DefaultFont;
                var lrt = labelGo.GetComponent<RectTransform>();
                lrt.anchorMin = lrt.anchorMax = lrt.pivot = new Vector2(0, 1);
                lrt.sizeDelta = new Vector2(30, 14);
                lrt.anchoredPosition = new Vector2(2, -(2 + i * 18));

                var barBgGo = new GameObject($"StatBg_{statNames[i]}", typeof(RectTransform), typeof(Image));
                barBgGo.transform.SetParent(statBg.transform, false);
                var bbg = barBgGo.GetComponent<RectTransform>();
                bbg.anchorMin = bbg.anchorMax = bbg.pivot = new Vector2(0, 1);
                bbg.sizeDelta = new Vector2(leftW - 70, 10);
                bbg.anchoredPosition = new Vector2(34, -(4 + i * 18));
                barBgGo.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.1f);

                var barFillGo = new GameObject($"StatFill_{statNames[i]}", typeof(RectTransform), typeof(Image));
                barFillGo.transform.SetParent(barBgGo.transform, false);
                var bfr = barFillGo.GetComponent<RectTransform>();
                bfr.anchorMin = Vector2.zero; bfr.anchorMax = new Vector2(0, 1);
                bfr.pivot = new Vector2(0, 0.5f);
                bfr.sizeDelta = Vector2.zero;
                barFillGo.GetComponent<Image>().color = new Color(0.3f, 0.6f, 0.3f);
            }

            // 右面板背景
            var rightPanel = new GameObject("RightPanel", typeof(RectTransform), typeof(Image));
            rightPanel.transform.SetParent(shellParent, false);
            var rpRt = rightPanel.GetComponent<RectTransform>();
            rpRt.anchorMin = new Vector2(1, 0); rpRt.anchorMax = new Vector2(1, 1);
            rpRt.pivot = new Vector2(1, 0);
            rpRt.anchoredPosition = new Vector2(-m, 0);
            rpRt.sizeDelta = new Vector2(rightW - m, 0);
            rightPanel.GetComponent<Image>().color = new Color(0.06f, 0.06f, 0.09f, 0.6f);
            rightPanel.GetComponent<Image>().raycastTarget = false;
        }

        static void CreateTopTabBar(Transform parent, float panelW)
        {
            var bar = new GameObject("TopTabBar", typeof(RectTransform), typeof(Image));
            bar.transform.SetParent(parent, false);
            var brt = bar.GetComponent<RectTransform>();
            brt.anchorMin = new Vector2(0, 1); brt.anchorMax = new Vector2(1, 1);
            brt.pivot = new Vector2(0.5f, 1);
            brt.sizeDelta = new Vector2(0, 36);
            brt.anchoredPosition = Vector2.zero;
            bar.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.15f, 1f);

            string[] tabs = { "装备容器", "角色", "制作", "地图", "设置" };
            float btnW = 100, btnH = 30;
            for (int i = 0; i < tabs.Length; i++)
            {
                var btnGo = new GameObject($"Tab_{tabs[i]}", typeof(RectTransform), typeof(Image), typeof(Button));
                btnGo.transform.SetParent(bar.transform, false);
                var brt2 = btnGo.GetComponent<RectTransform>();
                brt2.anchorMin = brt2.anchorMax = brt2.pivot = new Vector2(0, 0.5f);
                brt2.sizeDelta = new Vector2(btnW, btnH);
                brt2.anchoredPosition = new Vector2(10 + i * (btnW + 8), 0);
                btnGo.GetComponent<Image>().color = i == 0
                    ? new Color(0.2f, 0.4f, 0.6f)
                    : new Color(0.15f, 0.15f, 0.2f);

                var lblGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
                lblGo.transform.SetParent(btnGo.transform, false);
                StretchRT(lblGo.GetComponent<RectTransform>());
                var t = lblGo.GetComponent<Text>();
                t.font = UGUIBuilder.DefaultFont;
                t.fontSize = 13; t.alignment = TextAnchor.MiddleCenter;
                t.color = Color.white; t.raycastTarget = false; t.text = tabs[i];
            }
        }

        // ═══════════════════════════════════════════
        // Helpers
        // ═══════════════════════════════════════════

        static Canvas CreateChildCanvas(GameObject parent, string name, int sortOrder)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(parent.transform, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;
            return canvas;
        }

        static Text CreateText(Transform parent, string name, float w, float h, int fontSize,
            TextAnchor align, Vector2 anchor, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = UGUIBuilder.DefaultFont;
            t.fontSize = fontSize;
            t.alignment = align;
            t.raycastTarget = false;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = pos;
            return t;
        }

        static void CreateSlotText(Transform parent, string name, float w, float h, int fontSize,
            TextAnchor align, Vector2 anchor, Vector2 pos, Color color, string text)
        {
            var t = CreateText(parent, name, w, h, fontSize, align, anchor, pos);
            t.color = color;
            t.text = text;
        }

        static void CreateArrowText(Transform parent, string name, string text,
            Vector2 pivot, Vector2 pos)
        {
            var go = new GameObject(name, typeof(Text), typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = UGUIBuilder.DefaultFont;
            t.fontSize = 24;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = new Color(1, 1, 1, 0.5f);
            t.text = text;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0);
            rt.pivot = pivot;
            rt.sizeDelta = new Vector2(30, 50);
            rt.anchoredPosition = pos;
        }

        static void CreateBorder(Transform parent, int slotSize)
        {
            var go = new GameObject("Border", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.sizeDelta = rt.anchoredPosition = Vector2.zero;
            int bw = 2;
            MakeStrip(go.transform, "T", 0, 0, slotSize, bw);
            MakeStrip(go.transform, "B", 0, -(slotSize - bw), slotSize, bw);
            MakeStrip(go.transform, "L", 0, -bw, bw, slotSize - bw * 2);
            MakeStrip(go.transform, "R", slotSize - bw, -bw, bw, slotSize - bw * 2);
            go.SetActive(false);
        }

        static void MakeStrip(Transform parent, string name, float x, float y, float w, float h)
        {
            var strip = new GameObject(name, typeof(RectTransform), typeof(Image));
            strip.transform.SetParent(parent, false);
            var rt = strip.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, y);
            strip.GetComponent<Image>().color = Color.white;
        }

        static void CreateStatBar(Transform parent, string name, string label, float barW, float barH,
            float x, float y, Color barColor, Color bgColor, out Text labelOut, out Image fillOut)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1, 1);
            rt.sizeDelta = new Vector2(barW + 60, barH);
            rt.anchoredPosition = new Vector2(x, y);

            var lbl = CreateText(go.transform, "Label", 55, barH, 14, TextAnchor.MiddleLeft,
                new Vector2(0, 0.5f), new Vector2(2, 0));
            lbl.text = label; lbl.color = Color.white;
            labelOut = lbl;

            var sliderGo = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
            sliderGo.transform.SetParent(go.transform, false);
            var srt = sliderGo.GetComponent<RectTransform>();
            srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one;
            srt.offsetMin = new Vector2(58, 2); srt.offsetMax = new Vector2(-2, -2);
            var slider = sliderGo.GetComponent<Slider>();
            slider.minValue = 0; slider.maxValue = 100; slider.interactable = false;

            var bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(sliderGo.transform, false);
            var bgRt = bgGo.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
            bgRt.sizeDelta = bgRt.anchoredPosition = Vector2.zero;
            var bgImg = bgGo.GetComponent<Image>(); bgImg.color = bgColor;
            slider.targetGraphic = bgImg;

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(sliderGo.transform, false);
            var frt = fillGo.GetComponent<RectTransform>();
            frt.anchorMin = Vector2.zero; frt.anchorMax = new Vector2(0, 1);
            frt.pivot = new Vector2(0, 0.5f);
            frt.sizeDelta = frt.anchoredPosition = Vector2.zero;
            var fImg = fillGo.GetComponent<Image>(); fImg.color = barColor;
            slider.fillRect = frt;
            fillOut = fImg;
        }

        static Texture2D CreateCrosshairTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            int c = size / 2, outer = c - 2, inner = outer - 3;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int dx = x - c, dy = y - c, d2 = dx * dx + dy * dy;
                    tex.SetPixel(x, y, (d2 <= outer * outer && d2 >= inner * inner) || d2 <= 4
                        ? Color.white : Color.clear);
                }
            tex.Apply();
            return tex;
        }

        static int CleanupStandaloneUI()
        {
            int count = 0;
            var player = GameObject.FindWithTag("Player");

            // 扫描所有根 GameObject，如果带旧 HUD 组件且不在 Player 下就删
            var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var go in roots)
            {
                if (go == null || go == player) continue;
                bool hasHudComp = go.GetComponent<_Game.UI.SurvivalHUD>() != null
                    || go.GetComponent<_Game.UI.QuickItemBar>() != null
                    || go.GetComponent<_Game.UI.DecibelHUD>() != null
                    || go.GetComponent<_Game.UI.WeatherHUD>() != null
                    || go.GetComponent<_Game.UI.TopLeftHUD>() != null
                    || go.GetComponent<_Game.UI.CrosshairUI>() != null;
                if (hasHudComp)
                {
                    Object.DestroyImmediate(go);
                    count++;
                }
            }

            if (count > 0) Debug.Log($"[PreconfigureUI] 🧹 清理了 {count} 个孤立 UI 对象");
            return count;
        }


        // ═══════════════════════════════════════════
        // MainMenu 场景预构建
        // ═══════════════════════════════════════════

        [MenuItem("Tools/Preconfigure MainMenu")]
        public static void BuildMainMenu()
        {
            var root = GameObject.Find("MainMenuRoot");
            if (root == null) { Debug.LogError("[PreconfigureUI] ❌ MainMenu 场景中没有 MainMenuRoot！请先打开 MainMenu.scene"); return; }

            var menu = root.GetComponent<MainMenuUI>();
            if (menu == null) { Debug.LogError("[PreconfigureUI] ❌ MainMenuRoot 上没有 MainMenuUI 组件"); return; }

            BuildMainMenuUI(root, menu);
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("[PreconfigureUI] ✅ MainMenu UI 预构建完成");
        }

        static void BuildMainMenuUI(GameObject root, MainMenuUI menu)
        {
            // 清旧
            var oldCanvas = root.transform.Find("MainMenu_Canvas");
            if (oldCanvas != null) Object.DestroyImmediate(oldCanvas.gameObject);

            var canvas = CreateChildCanvas(root, "MainMenu_Canvas", 300);
            canvas.GetComponent<CanvasScaler>().matchWidthOrHeight = 0.5f;

            // 全屏背景
            var bgGo = new GameObject("BG", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(canvas.transform, false);
            StretchRT(bgGo.GetComponent<RectTransform>());
            bgGo.GetComponent<Image>().color = new Color(0.02f, 0.02f, 0.05f, 0.98f);

            // 主面板
            int panelW = 700, panelH = 620;
            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canvas.transform, false);
            CenterRT(panel.GetComponent<RectTransform>(), panelW, panelH);
            panel.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.08f, 1f);

            // 标题
            CreateUIText(panel.transform, "Title", "末日生存", 32, FontStyle.Bold, TextAnchor.MiddleCenter, panelW - 40, 50,
                new Vector2(0.5f, 1), new Vector2(0, -20));
            CreateUIText(panel.transform, "Subtitle", "选择存档", 14, FontStyle.Normal, TextAnchor.MiddleCenter, panelW - 40, 24,
                new Vector2(0.5f, 1), new Vector2(0, -60), new Color(0.5f, 0.5f, 0.5f));

            // 5 个槽位
            float slotH = 70, slotGap = 8;
            var saveRoot = new GameObject("SaveSlots", typeof(RectTransform));
            saveRoot.transform.SetParent(panel.transform, false);
            var sr = saveRoot.GetComponent<RectTransform>();
            sr.anchorMin = sr.anchorMax = sr.pivot = new Vector2(0, 1);
            sr.anchoredPosition = new Vector2(20, -100);
            sr.sizeDelta = new Vector2(panelW - 40, 5 * (slotH + slotGap));

            for (int i = 0; i < 5; i++)
            {
                var slotGo = new GameObject($"Slot_{i}", typeof(RectTransform), typeof(Image));
                slotGo.transform.SetParent(saveRoot.transform, false);
                var slotRect = slotGo.GetComponent<RectTransform>();
                slotRect.anchorMin = new Vector2(0, 1); slotRect.anchorMax = new Vector2(1, 1);
                slotRect.pivot = new Vector2(0, 1);
                slotRect.anchoredPosition = new Vector2(0, -i * (slotH + slotGap));
                slotRect.sizeDelta = new Vector2(0, slotH);
                slotGo.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.12f, 1f);
            }

            // 退出按钮
            CreateUIButton(panel.transform, "ExitBtn", "退出游戏", new Color(0.3f, 0.1f, 0.1f), 200, 36,
                new Vector2(0.5f, 0), new Vector2(0, -panelH + 50));

            // 隐藏 Canvas（Start 时由代码控制显示）
            canvas.gameObject.SetActive(false);
        }

        static void CreateUIText(Transform parent, string name, string txt, int fontSize,
            FontStyle style, TextAnchor align, float w, float h, Vector2 anchor, Vector2 pos, Color? c = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = UGUIBuilder.DefaultFont;
            t.fontSize = fontSize; t.fontStyle = style; t.alignment = align;
            t.color = c ?? Color.white; t.raycastTarget = false; t.text = txt;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = pos;
        }

        static void CreateUIButton(Transform parent, string name, string label, Color bg,
            float w, float h, Vector2 anchor, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = bg;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = pos;

            var lblGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            lblGo.transform.SetParent(go.transform, false);
            var t = lblGo.GetComponent<Text>();
            t.font = UGUIBuilder.DefaultFont;
            t.fontSize = 13; t.fontStyle = FontStyle.Bold; t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white; t.raycastTarget = false; t.text = label;
            StretchRT(lblGo.GetComponent<RectTransform>());
        }

        static void StretchRT(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        static void CenterRT(RectTransform rt, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = Vector2.zero;
        }

        static string GetKeyLabel(int i) => i < 9 ? (i + 1).ToString() : "0";
        static bool FindChild(GameObject parent, string name) => parent.transform.Find(name) != null;
    }
}
