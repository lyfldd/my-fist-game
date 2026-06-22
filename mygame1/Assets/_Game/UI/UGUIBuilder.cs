using System;
using UnityEngine;
using UnityEngine.UI;

namespace _Game.UI
{
    /// <summary>
    /// UGUI 统一构建器。所有 UI 动态创建走这里，减少重复代码。
    /// 未来可改为加载 prefab 而不改调用侧。
    ///
    /// 用法：
    ///   var canvas = UGUIBuilder.CreateCanvas("MyCanvas", 100);
    ///   var panel = UGUIBuilder.CreatePanel("Panel", canvas.transform, 600, 400);
    ///   var text  = UGUIBuilder.CreateText("Title", panel.transform, "标题", 24, FontStyle.Bold);
    ///   var btn   = UGUIBuilder.CreateButton("Btn", panel.transform, "确定", new Color(0.15f,0.4f,0.15f), 120, 36);
    /// </summary>
    public static class UGUIBuilder
    {
        static Font _cachedFont;

        /// <summary>获取默认字体（Arial 14pt，懒加载）</summary>
        public static Font DefaultFont
        {
            get
            {
                if (_cachedFont == null)
                    _cachedFont = Font.CreateDynamicFontFromOSFont("Arial", 14);
                return _cachedFont;
            }
        }

        // ═══════════════════════════════════════════
        // Canvas
        // ═══════════════════════════════════════════

        /// <summary>创建 ScreenSpaceOverlay Canvas</summary>
        public static Canvas CreateCanvas(string name, int sortOrder = 100)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            return canvas;
        }

        // ═══════════════════════════════════════════
        // Panel / Background
        // ═══════════════════════════════════════════

        /// <summary>创建全屏背景 Image</summary>
        public static GameObject CreateFullscreenBG(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Stretch(go.GetComponent<RectTransform>());
            go.GetComponent<Image>().color = color;
            go.GetComponent<Image>().raycastTarget = false;
            return go;
        }

        /// <summary>创建居中面板</summary>
        public static GameObject CreateCenteredPanel(string name, Transform parent, float w, float h, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Center(go.GetComponent<RectTransform>(), w, h);
            go.GetComponent<Image>().color = color;
            return go;
        }

        /// <summary>创建全尺寸拉伸面板</summary>
        public static GameObject CreateStretchPanel(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Stretch(go.GetComponent<RectTransform>());
            go.GetComponent<Image>().color = color;
            return go;
        }

        // ═══════════════════════════════════════════
        // Text
        // ═══════════════════════════════════════════

        /// <summary>创建 Text</summary>
        public static Text CreateText(string name, Transform parent, string text, int fontSize = 14,
            FontStyle style = FontStyle.Normal, TextAnchor align = TextAnchor.MiddleCenter,
            float w = 200, float h = 30, Color? color = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = DefaultFont;
            t.fontSize = fontSize;
            t.fontStyle = style;
            t.alignment = align;
            t.color = color ?? Color.white;
            t.raycastTarget = false;
            t.text = text;
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(w, h);
            return t;
        }

        /// <summary>创建 Text 并锚定到指定位置</summary>
        public static Text CreateTextAnchored(string name, Transform parent, string text,
            Vector2 anchor, Vector2 pos, float w, float h, int fontSize = 14,
            FontStyle style = FontStyle.Normal, TextAnchor align = TextAnchor.MiddleCenter,
            Color? color = null)
        {
            var t = CreateText(name, parent, text, fontSize, style, align, w, h, color);
            AnchorAt(t.rectTransform, anchor, pos);
            return t;
        }

        // ═══════════════════════════════════════════
        // Button
        // ═══════════════════════════════════════════

        /// <summary>创建按钮</summary>
        public static Button CreateButton(string name, Transform parent, string label,
            Color bgColor, float w = 120, float h = 36, int fontSize = 13)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = bgColor;
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(w, h);

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            var t = labelGo.AddComponent<Text>();
            t.font = DefaultFont;
            t.fontSize = fontSize;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            t.raycastTarget = false;
            t.text = label;
            Stretch(labelGo.GetComponent<RectTransform>());

            return go.GetComponent<Button>();
        }

        // ═══════════════════════════════════════════
        // Bar / Slider (简版进度条)
        // ═══════════════════════════════════════════

        /// <summary>创建进度条（背景 + 填充，返回 fill Image）</summary>
        public static Image CreateProgressBar(string name, Transform parent, float w, float h,
            Color bgColor, Color fillColor, out GameObject barGo, out Image bgImage)
        {
            barGo = new GameObject(name, typeof(RectTransform));
            barGo.transform.SetParent(parent, false);
            barGo.GetComponent<RectTransform>().sizeDelta = new Vector2(w, h);

            var bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(barGo.transform, false);
            bgImage = bgGo.GetComponent<Image>();
            bgImage.color = bgColor;
            Stretch(bgGo.GetComponent<RectTransform>());

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(barGo.transform, false);
            var fill = fillGo.GetComponent<Image>();
            fill.color = fillColor;
            var fillRect = fillGo.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(1, 1);
            fillRect.pivot = new Vector2(0, 0.5f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            return fill;
        }

        /// <summary>设置进度条填充比例（0~1）</summary>
        public static void SetBarFill(Image fillImage, float percent)
        {
            fillImage.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(percent), 1);
        }

        // ═══════════════════════════════════════════
        // ScrollView
        // ═══════════════════════════════════════════

        /// <summary>创建 ScrollRect（返回 content Transform 用于添加子元素）</summary>
        public static RectTransform CreateScrollView(string name, Transform parent, float w, float h,
            out GameObject scrollGo)
        {
            scrollGo = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollGo.transform.SetParent(parent, false);
            scrollGo.GetComponent<RectTransform>().sizeDelta = new Vector2(w, h);

            var vp = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            vp.transform.SetParent(scrollGo.transform, false);
            Stretch(vp.GetComponent<RectTransform>());
            vp.GetComponent<Image>().color = Color.clear;

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(vp.transform, false);
            var cr = content.GetComponent<RectTransform>();
            cr.anchorMin = new Vector2(0, 1);
            cr.anchorMax = new Vector2(1, 1);
            cr.pivot = new Vector2(0.5f, 1);
            cr.sizeDelta = new Vector2(0, 0);
            cr.anchoredPosition = Vector2.zero;

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.viewport = vp.GetComponent<RectTransform>();
            scroll.content = cr;
            scroll.horizontal = false;
            scroll.vertical = true;

            return cr;
        }

        // ═══════════════════════════════════════════
        // RectTransform 工具
        // ═══════════════════════════════════════════

        /// <summary>全拉伸（填满父容器）</summary>
        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>居中，固定尺寸</summary>
        public static void Center(RectTransform rt, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = Vector2.zero;
        }

        /// <summary>锚定到指定点</summary>
        public static void AnchorAt(RectTransform rt, Vector2 anchor, Vector2 pos)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos;
        }

        /// <summary>设置固定尺寸</summary>
        public static void SetSize(RectTransform rt, float w, float h)
        {
            rt.sizeDelta = new Vector2(w, h);
        }
    }
}
