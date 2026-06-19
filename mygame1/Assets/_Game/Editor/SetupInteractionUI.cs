using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 一键创建交互提示 UI
/// 用法：Unity 菜单 → Tools → Setup Interaction UI
/// </summary>
public class SetupInteractionUI
{
    [MenuItem("Tools/Setup Interaction UI")]
    public static void Setup()
    {
        var canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("场景中没有 Canvas！");
            return;
        }

        // 如果已有则跳过
        var existing = canvas.transform.Find("InteractionPrompt");
        if (existing != null)
        {
            return;
        }

        // 创建 PromptPanel
        var panel = new GameObject("InteractionPrompt", typeof(RectTransform), typeof(CanvasGroup));
        panel.transform.SetParent(canvas.transform, false);
        var panelRt = panel.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 0.5f);
        panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = new Vector2(300, 80);

        // CanvasGroup 用于淡入淡出
        var cg = panel.GetComponent<CanvasGroup>();
        cg.alpha = 0f;
        cg.interactable = false;
        cg.blocksRaycasts = false;

        // 提示文字
        var textObj = new GameObject("PromptText", typeof(RectTransform), typeof(Text));
        textObj.transform.SetParent(panel.transform, false);
        var textRt = textObj.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = new Vector2(0, -30);
        var text = textObj.GetComponent<Text>();
        text.text = "按 E 交互";
        text.fontSize = 16;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // 进度条背景
        var progressBg = new GameObject("ProgressBarBG", typeof(RectTransform), typeof(Image));
        progressBg.transform.SetParent(panel.transform, false);
        var bgRt = progressBg.GetComponent<RectTransform>();
        bgRt.anchorMin = new Vector2(0.1f, 0);
        bgRt.anchorMax = new Vector2(0.9f, 0);
        bgRt.offsetMin = new Vector2(0, 5);
        bgRt.offsetMax = new Vector2(0, 25);
        var bgImg = progressBg.GetComponent<Image>();
        bgImg.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

        // 进度条前景
        var progressBar = new GameObject("ProgressBar", typeof(RectTransform), typeof(Image));
        progressBar.transform.SetParent(progressBg.transform, false);
        var pbRt = progressBar.GetComponent<RectTransform>();
        pbRt.anchorMin = Vector2.zero;
        pbRt.anchorMax = Vector2.one;
        pbRt.offsetMin = Vector2.zero;
        pbRt.offsetMax = Vector2.zero;
        var pbImg = progressBar.GetComponent<Image>();
        pbImg.color = new Color(0.3f, 0.7f, 0.3f);
        pbImg.type = Image.Type.Filled;
        pbImg.fillMethod = Image.FillMethod.Horizontal;
        pbImg.fillAmount = 0f;

        // 默认隐藏进度条
        progressBg.SetActive(false);

        // 给 Player 添加交互组件
        var player = GameObject.FindWithTag("Player") ?? GameObject.Find("Player");
        if (player != null)
        {
            var interaction = player.GetComponent<_Game.Systems.Interaction.PlayerInteraction>();
            if (interaction == null)
            {
                interaction = player.AddComponent<_Game.Systems.Interaction.PlayerInteraction>();
                EditorUtility.SetDirty(interaction);
            }
        }

        // 创建测试箱子
        CreateTestChest();

    }

    static void CreateTestChest()
    {
        var existing = GameObject.Find("TestChest");
        if (existing != null)
        {
            return;
        }

        var chest = GameObject.CreatePrimitive(PrimitiveType.Cube);
        chest.name = "TestChest";
        chest.transform.position = new Vector3(0, 0.5f, 3);  // 玩家正前方 3 米
        chest.transform.localScale = new Vector3(1, 1, 1);

        var interactable = chest.AddComponent<_Game.Systems.Interaction.TestInteractable>();
        interactable.prompt = "搜索箱子";
        interactable.holdTime = 2f;

    }
}
