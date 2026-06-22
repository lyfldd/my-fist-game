using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using _Game.Core;
using _Game.Systems.SaveLoad;

namespace _Game.UI
{
    /// <summary>
    /// 主菜单存档选择界面。UGUI 全屏覆盖，高优先级 Canvas。
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        public static MainMenuUI Instance { get; private set; }

        private GameObject _canvasGo;
        private GameObject _panelGo;
        private bool _visible = true;
        private readonly List<GameObject> _slotRows = new List<GameObject>();

        private readonly Color bgColor      = new Color(0.02f, 0.02f, 0.05f, 0.98f);
        private readonly Color panelColor   = new Color(0.05f, 0.05f, 0.08f, 1f);
        private readonly Color slotColor    = new Color(0.08f, 0.08f, 0.12f, 1f);
        private readonly Color btnColor     = new Color(0.15f, 0.4f, 0.15f, 1f);
        private readonly Color delColor     = new Color(0.5f, 0.15f, 0.15f, 1f);
        private readonly Color exitColor    = new Color(0.3f, 0.1f, 0.1f, 1f);
        private readonly Color dimTextColor = new Color(0.5f, 0.5f, 0.5f, 1f);

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            if (Camera.main == null)
            {
                var camGo = new GameObject("MenuCamera", typeof(Camera));
                camGo.transform.SetParent(transform, false);
            }
            try { CreateUI(); } catch (Exception e) { Debug.LogError($"[MainMenuUI] 创建失败: {e}"); }
        }

        void Update()
        {
            if (_canvasGo != null)
            {
                bool show = _visible && UIModeConfig.UseUGUI;
                if (_canvasGo.activeSelf != show) { _canvasGo.SetActive(show); if (show) RefreshSlots(); }
            }
        }

        void CreateUI()
        {
            // 优先使用 PreconfigureUI 预构建的
            var existingCanvas = transform.Find("MainMenu_Canvas");
            if (existingCanvas != null)
            {
                _canvasGo = existingCanvas.gameObject;
                _panelGo = _canvasGo.transform.Find("Panel")?.gameObject;
                var slotsRoot = _panelGo?.transform.Find("SaveSlots");
                if (slotsRoot != null)
                    for (int i = 0; i < slotsRoot.childCount; i++)
                        _slotRows.Add(slotsRoot.GetChild(i).gameObject);
                var preExitBtn = _panelGo?.transform.Find("ExitBtn")?.GetComponent<Button>();
                if (preExitBtn != null) preExitBtn.onClick.AddListener(ExitGame);
                RefreshSlots();
                return;
            }

            // 兜底：运行时创建
            var canvas = UGUIBuilder.CreateCanvas("MainMenu_Canvas", 300);
            canvas.transform.SetParent(transform, false);
            canvas.GetComponent<CanvasScaler>().matchWidthOrHeight = 0.5f;
            _canvasGo = canvas.gameObject;
            _canvasGo.SetActive(false);

            UGUIBuilder.CreateFullscreenBG("BG", _canvasGo.transform, bgColor);

            int panelW = 700, panelH = 620;
            _panelGo = UGUIBuilder.CreateCenteredPanel("Panel", _canvasGo.transform, panelW, panelH, panelColor);

            var title = UGUIBuilder.CreateTextAnchored("Title", _panelGo.transform, "末日生存",
                new Vector2(0.5f, 1), new Vector2(0, -20), panelW - 40, 50, 32, FontStyle.Bold);
            var sub = UGUIBuilder.CreateTextAnchored("Subtitle", _panelGo.transform, "选择存档",
                new Vector2(0.5f, 1), new Vector2(0, -60), panelW - 40, 24, 14);
            sub.color = dimTextColor;

            float slotH = 70, slotGap = 8;
            var saveRoot = new GameObject("SaveSlots", typeof(RectTransform));
            saveRoot.transform.SetParent(_panelGo.transform, false);
            var srRect = saveRoot.GetComponent<RectTransform>();
            srRect.anchorMin = srRect.anchorMax = srRect.pivot = new Vector2(0, 1);
            srRect.anchoredPosition = new Vector2(20, -100);
            srRect.sizeDelta = new Vector2(panelW - 40, SaveService.MAX_SLOTS * (slotH + slotGap));

            for (int i = 0; i < SaveService.MAX_SLOTS; i++)
            {
                var slotGo = new GameObject($"Slot_{i}", typeof(RectTransform), typeof(Image));
                slotGo.transform.SetParent(saveRoot.transform, false);
                var slotRect = slotGo.GetComponent<RectTransform>();
                slotRect.anchorMin = new Vector2(0, 1); slotRect.anchorMax = new Vector2(1, 1);
                slotRect.pivot = new Vector2(0, 1);
                slotRect.anchoredPosition = new Vector2(0, -i * (slotH + slotGap));
                slotRect.sizeDelta = new Vector2(0, slotH);
                slotGo.GetComponent<Image>().color = slotColor;
                _slotRows.Add(slotGo);
            }

            var exitBtn = UGUIBuilder.CreateButton("ExitBtn", _panelGo.transform, "退出游戏", exitColor, 200, 36);
            UGUIBuilder.AnchorAt(exitBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0, -panelH + 50));
            exitBtn.onClick.AddListener(ExitGame);

            RefreshSlots();
        }

        void RefreshSlots()
        {
            var metas = SaveService.GetAllMetas();
            for (int i = 0; i < SaveService.MAX_SLOTS && i < _slotRows.Count; i++)
            {
                var slotGo = _slotRows[i];
                foreach (Transform t in slotGo.transform) Destroy(t.gameObject);

                var meta = metas[i];
                bool hasSave = meta != null;
                float leftX = 12;

                var label = UGUIBuilder.CreateTextAnchored("Label", slotGo.transform, $"存档 {i + 1}",
                    new Vector2(0, 1), new Vector2(leftX, -8), 100, 28, 16, FontStyle.Bold, TextAnchor.MiddleLeft);

                if (hasSave && !meta.isDead)
                {
                    var info = UGUIBuilder.CreateTextAnchored("Info", slotGo.transform,
                        $"{meta.GetDescription()}\n{meta.saveDateTime}",
                        new Vector2(0, 1), new Vector2(leftX, -38), 200, 40, 13,
                        FontStyle.Normal, TextAnchor.MiddleLeft);

                    int si = i;
                    var loadBtn = MakeSlotBtn("LoadBtn", "继续", btnColor, 80, 32, new Vector2(1, 0.5f), new Vector2(-180, 0));
                    loadBtn.transform.SetParent(slotGo.transform, false);
                    loadBtn.onClick.AddListener(() => LoadSlot(si));

                    var delBtn = MakeSlotBtn("DelBtn", "删除", delColor, 70, 32, new Vector2(1, 0.5f), new Vector2(-90, 0));
                    delBtn.transform.SetParent(slotGo.transform, false);
                    delBtn.onClick.AddListener(() => DeleteSlot(si));
                }
                else if (hasSave && meta.isDead)
                {
                    var deadInfo = UGUIBuilder.CreateTextAnchored("DeadInfo", slotGo.transform,
                        $"☠ {meta.GetDescription()}\n{meta.saveDateTime}",
                        new Vector2(0, 1), new Vector2(leftX, -38), 250, 40, 13,
                        FontStyle.Normal, TextAnchor.MiddleLeft, new Color(0.7f, 0.2f, 0.2f));

                    int si = i;
                    var delBtn = MakeSlotBtn("DelBtn", "删除", delColor, 70, 32, new Vector2(1, 0.5f), new Vector2(-90, 0));
                    delBtn.transform.SetParent(slotGo.transform, false);
                    delBtn.onClick.AddListener(() => DeleteSlot(si));
                }
                else
                {
                    var empty = UGUIBuilder.CreateTextAnchored("Empty", slotGo.transform, "空槽位",
                        new Vector2(0, 1), new Vector2(leftX, -38), 200, 20, 13,
                        FontStyle.Normal, TextAnchor.MiddleLeft, dimTextColor);

                    int si = i;
                    var newBtn = MakeSlotBtn("NewBtn", "新游戏", btnColor, 90, 32, new Vector2(1, 0.5f), new Vector2(-100, 0));
                    newBtn.transform.SetParent(slotGo.transform, false);
                    newBtn.onClick.AddListener(() => NewGame(si));
                }
            }
        }

        Button MakeSlotBtn(string name, string label, Color bg, float w, float h, Vector2 anchor, Vector2 pos)
        {
            var btn = UGUIBuilder.CreateButton(name, null, label, bg, w, h);
            UGUIBuilder.AnchorAt(btn.GetComponent<RectTransform>(), anchor, pos);
            return btn;
        }

        // ============================================================
        // 操作
        // ============================================================

        void NewGame(int slot)
        {
            SaveLoadManager.Instance._pendingSlot = slot;
            SaveLoadManager.Instance._pendingLoad = false;
            SceneManager.sceneLoaded += OnGameSceneLoaded;
            SceneManager.LoadScene("MainScene");
        }

        void LoadSlot(int slot)
        {
            SaveLoadManager.Instance._pendingSlot = slot;
            SaveLoadManager.Instance._pendingLoad = true;
            SceneManager.sceneLoaded += OnGameSceneLoaded;
            SceneManager.LoadScene("MainScene");
        }

        void OnGameSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SceneManager.sceneLoaded -= OnGameSceneLoaded;
            if (SaveLoadManager.Instance._pendingLoad)
                SaveLoadManager.Instance.LoadGame(SaveLoadManager.Instance._pendingSlot);
            else
                SaveLoadManager.Instance.NewGame(SaveLoadManager.Instance._pendingSlot);
            Destroy(gameObject);
        }

        void DeleteSlot(int slot) { SaveService.DeleteSlot(slot); RefreshSlots(); }

        void ExitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void Show() { _visible = true; RefreshSlots(); }
        public void Hide() { _visible = false; if (_canvasGo != null) _canvasGo.SetActive(false); }
    }
}
