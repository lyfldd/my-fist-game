using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// 一键创建总览面板 UI
/// 用法：Unity 菜单 → Tools → Setup Overview Panel
/// </summary>
public class SetupOverviewPanel
{
    [MenuItem("Tools/Setup Overview Panel")]
    public static void Setup()
    {
        var canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("场景中没有 Canvas！");
            return;
        }

        var invUI = Object.FindObjectOfType<_Game.UI.InventoryUI>();
        if (invUI == null)
        {
            Debug.LogError("场景中没有 InventoryUI 组件！");
            return;
        }

        // ===== 如果已有 OverviewPanel，先删掉重建 =====
        var existing = canvas.transform.Find("OverviewPanel");
        if (existing != null)
            GameObject.DestroyImmediate(existing.gameObject);

        // ===== 创建 OverviewPanel =====
        var panel = new GameObject("OverviewPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(canvas.transform, false);
        var panelRt = panel.GetComponent<RectTransform>();
        panelRt.anchorMin = Vector2.zero;
        panelRt.anchorMax = Vector2.one;
        panelRt.offsetMin = Vector2.zero;
        panelRt.offsetMax = Vector2.zero;
        var panelImg = panel.GetComponent<Image>();
        panelImg.color = new Color(0, 0, 0, 0.7f); // 半透明黑底

        // ===== OverviewInfoText =====
        var infoText = new GameObject("OverviewInfoText", typeof(RectTransform), typeof(Text));
        infoText.transform.SetParent(panel.transform, false);
        var infoRt = infoText.GetComponent<RectTransform>();
        infoRt.anchorMin = new Vector2(0, 0.85f);
        infoRt.anchorMax = new Vector2(1, 1);
        infoRt.offsetMin = new Vector2(10, 0);
        infoRt.offsetMax = new Vector2(-10, -10);
        var infoTxt = infoText.GetComponent<Text>();
        infoTxt.text = "<b>背包总览</b>";
        infoTxt.fontSize = 16;
        infoTxt.alignment = TextAnchor.UpperCenter;
        infoTxt.color = Color.white;
        infoTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // ===== OverviewGridContainer =====
        var gridContainer = new GameObject("OverviewGridContainer", typeof(RectTransform));
        gridContainer.transform.SetParent(panel.transform, false);
        var gridRt = gridContainer.GetComponent<RectTransform>();
        gridRt.anchorMin = new Vector2(0, 0);
        gridRt.anchorMax = new Vector2(1, 0.85f);
        gridRt.offsetMin = new Vector2(10, 10);
        gridRt.offsetMax = new Vector2(-10, -5);

        // ===== 绑定到 InventoryUI =====
        Undo.RecordObject(invUI, "Setup Overview Panel");
        invUI.overviewPanel = panel;
        invUI.overviewInfoText = infoTxt;
        invUI.overviewGridContainer = gridContainer;

        // ===== 自动绑定 QuickPanel =====
        var quickPanel = canvas.transform.Find("QuickPanel") ?? canvas.transform.Find("InventoryPanel");
        if (quickPanel != null)
        {
            invUI.quickPanel = quickPanel.gameObject;
            invUI.quickInfoText = quickPanel.GetComponentInChildren<Text>();
            invUI.quickGridContainer = quickPanel.Find("GridContainer")?.gameObject;
        }

        EditorUtility.SetDirty(invUI);
        Debug.Log("总览面板创建完成，已自动绑定到 InventoryUI！");
    }
}
