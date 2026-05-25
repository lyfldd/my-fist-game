using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using _Game.Config;
using _Game.Core;
using _Game.Systems.Inventory;
using _Game.Systems.Interaction;

namespace _Game.Systems.WorldContainer
{
    /// <summary>
    /// 世界容器组件 — 挂在场景中任意有 Collider 的 GameObject 上。
    /// 两态：Unopened → Opened，永远可交互。
    /// 容器生命周期由 ContainerRegistry 懒加载管理，ChunkManager 控制刷新。
    /// </summary>
    public class WorldContainer : MonoBehaviour, IInteractable
    {
        [Header("容器配置")]
        [Tooltip("绑定 Loot 配置资产（掉落表 + 格子尺寸 + 搜索时间）")]
        public ContainerLootProfile profile;

        [Header("容器ID（场景中唯一，由编辑器设置）")]
        public int containerId;

        // ===== IInteractable — 动态属性 =====

        public string InteractionPrompt
        {
            get
            {
                string name = profile != null ? profile.displayName : "容器";
                return IsOpened ? $"打开 {name}" : $"搜索 {name}";
            }
        }

        public float InteractionTime
        {
            get
            {
                if (!IsOpened && profile != null)
                    return profile.searchTime;
                return 0f;
            }
        }

        public bool IsInteractable => true;

        // ===== 内部状态 =====

        private bool IsOpened => ContainerRegistry.Instance != null
            && ContainerRegistry.Instance.IsOpened(containerId);

        private bool _isSearching;

        // ===== 进度条 UI =====

        private Canvas _progressCanvas;
        private Slider _progressSlider;

        void Awake()
        {
            if (profile == null)
            {
                Debug.LogError($"WorldContainer [{gameObject.name}]: 未配置 ContainerLootProfile，容器将不可用", this);
            }

            BuildProgressUI();
        }

        public void OnInteract(GameObject interactor)
        {
            if (IsOpened)
            {
                OpenContainerWindow();
            }
            else if (!_isSearching)
            {
                StartCoroutine(SearchRoutine());
            }
        }

        // ===== 搜索协程 =====

        private IEnumerator SearchRoutine()
        {
            _isSearching = true;
            float searchTime = profile != null ? profile.searchTime : 1f;

            if (_progressCanvas != null)
            {
                _progressCanvas.gameObject.SetActive(true);
                if (_progressSlider != null)
                    _progressSlider.value = 0f;
            }

            float elapsed = 0f;
            while (elapsed < searchTime)
            {
                elapsed += UnityEngine.Time.deltaTime;
                if (_progressSlider != null)
                    _progressSlider.value = elapsed / searchTime;
                yield return null;
            }

            if (_progressCanvas != null)
                _progressCanvas.gameObject.SetActive(false);

            // 懒加载容器 + 生成掉落
            var registry = ContainerRegistry.Instance;
            var container = registry != null ? registry.GetOrCreate(containerId) : null;

            if (container != null)
            {
                bool isEmpty = Random.value < (profile != null ? profile.emptyChance : 0f);
                if (!isEmpty && profile != null && profile.lootTable != null)
                {
                    var loot = profile.lootTable.GenerateLoot(profile.minLootTypes, profile.maxLootTypes);
                    foreach (var (item, count) in loot)
                    {
                        container.AddItem(item, count, float.MaxValue);
                    }
                }
            }

            if (registry != null)
            {
                var timeManager = FindObjectOfType<_Game.Systems.Time.TimeManager>();
                registry.MarkOpened(containerId, timeManager != null ? timeManager.CurrentDay : 0);
            }

            _isSearching = false;

            EventBus.Publish(new ContainerSearchedEvent(gameObject, profile != null ? profile.displayName : ""));
            OpenContainerWindow();
        }

        // ===== 打开容器窗口 =====

        private void OpenContainerWindow()
        {
            var container = ContainerRegistry.Instance?.GetOrCreate(containerId);
            if (container == null) return;

            var ui = ContainerWindowUI.Instance;
            if (ui != null)
            {
                string title = profile != null ? profile.displayName : "容器";
                ui.OpenContainer(this, container, title);
            }

            EventBus.Publish(new ContainerOpenedEvent(gameObject, profile != null ? profile.displayName : ""));

            var invUI = FindObjectOfType<_Game.UI.InventoryUI>();
            if (invUI != null)
                invUI.ShowOverview();
        }

        public void OnContainerWindowClosed()
        {
            // 两态设计：容器关闭后不切状态，永远可交互
        }

        // ===== 进度条 UI 构建（世界空间） =====

        private void BuildProgressUI()
        {
            var canvasGo = new GameObject("SearchProgressCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            _progressCanvas = canvasGo.GetComponent<Canvas>();
            _progressCanvas.renderMode = RenderMode.WorldSpace;
            canvasGo.GetComponent<GraphicRaycaster>().enabled = false;

            var canvasRt = canvasGo.GetComponent<RectTransform>();
            canvasRt.sizeDelta = new Vector2(2f, 0.3f);
            canvasRt.localPosition = new Vector3(0, 1.2f, 0);

            var sliderGo = new GameObject("Slider", typeof(Slider));
            sliderGo.transform.SetParent(canvasGo.transform, false);
            _progressSlider = sliderGo.GetComponent<Slider>();
            _progressSlider.interactable = false;
            _progressSlider.minValue = 0f;
            _progressSlider.maxValue = 1f;
            _progressSlider.value = 0f;

            var sliderRt = sliderGo.GetComponent<RectTransform>();
            sliderRt.anchorMin = Vector2.zero;
            sliderRt.anchorMax = Vector2.one;
            sliderRt.offsetMin = Vector2.zero;
            sliderRt.offsetMax = Vector2.zero;

            var bgGo = new GameObject("Background", typeof(Image));
            bgGo.transform.SetParent(sliderGo.transform, false);
            bgGo.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f, 0.75f);
            var bgRt = bgGo.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;

            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(sliderGo.transform, false);
            var faRt = fillArea.GetComponent<RectTransform>();
            faRt.anchorMin = Vector2.zero;
            faRt.anchorMax = Vector2.one;
            faRt.offsetMin = new Vector2(2, 2);
            faRt.offsetMax = new Vector2(-2, -2);

            var fillGo = new GameObject("Fill", typeof(Image));
            fillGo.transform.SetParent(fillArea.transform, false);
            fillGo.GetComponent<Image>().color = new Color(0.95f, 0.7f, 0.1f, 1f);
            var fillRt = fillGo.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;

            _progressSlider.fillRect = fillRt;
            _progressSlider.targetGraphic = fillGo.GetComponent<Image>();

            _progressCanvas.gameObject.SetActive(false);
        }
    }
}
