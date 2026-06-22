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

                CreateBorder(slot.transform, slotSize);
            }

            // Arrows
            CreateArrowText(canvas.transform, "ArrowLeft", "<", new Vector2(1, 0.5f),
                new Vector2(-totalWidth / 2 - 8, slotSize / 2 + 20));
            CreateArrowText(canvas.transform, "ArrowRight", ">", new Vector2(0, 0.5f),
                new Vector2(totalWidth / 2 + 8, slotSize / 2 + 20));
            return 1;
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
