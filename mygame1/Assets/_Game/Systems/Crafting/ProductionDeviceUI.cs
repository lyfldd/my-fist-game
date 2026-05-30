using System.Collections.Generic;
using UnityEngine;
using _Game.Config;
using _Game.Core;
using _Game.Systems.Power;

namespace _Game.Systems.Crafting
{
    /// <summary>
    /// 生产设备交互面板 UI（OnGUI 原型）。
    /// 流程：E键设备 → DeviceOpenedEvent → 显示面板
    ///       查看配方/取出产物/补充材料
    ///       Esc → DeviceClosedEvent → 隐藏面板
    /// </summary>
    public class ProductionDeviceUI : MonoBehaviour
    {
        [Header("布局")]
        public float panelWidth = 540f;
        public float panelHeight = 520f;
        public float leftWidth = 260f;
        public float rightWidth = 260f;
        public float buttonHeight = 28f;
        public float padding = 10f;

        [Header("颜色")]
        public Color bgColor = new Color(0.06f, 0.06f, 0.06f, 0.97f);
        public Color headerColor = new Color(0.15f, 0.15f, 0.15f, 1f);
        public Color sectionBgColor = new Color(0.12f, 0.12f, 0.12f, 0.8f);
        public Color btnColor = new Color(0f, 0.5f, 0f, 1f);
        public Color fuelColor = new Color(0.9f, 0.55f, 0.1f, 1f);
        public Color textColor = Color.white;
        public Color dimTextColor = new Color(0.65f, 0.65f, 0.65f, 1f);

        ProductionDevice _currentDevice;
        ProductionDeviceData _deviceData;
        Inventory.Inventory _playerInv;
        bool _isVisible;
        int _selectedRecipeIdx = -1;
        Vector2 _recipeScrollPos, _outputScrollPos, _inputScrollPos, _linkScrollPos;
        ProductionDevice[] _nearbyDevices;
        float _nearbyScanTimer;

        GUIStyle _headerStyle, _sectionHeaderStyle, _labelStyle, _dimStyle;
        GUIStyle _btnStyle, _disabledBtnStyle, _closeBtnStyle, _greenBtnStyle;
        GUIStyle _bgBoxStyle;
        bool _stylesReady;

        static ProductionDeviceUI _instance;

        void Awake()
        {
            // 单例：防止 GameBootstrap 自动添加 + 场景已有 → 双实例同时画
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning($"[ProductionDeviceUI] 检测到重复实例，销毁 {gameObject.name} 上的副本");
                Destroy(this);
                return;
            }
            _instance = this;

            _playerInv = Object.FindObjectOfType<Inventory.Inventory>();
            // 强制不透明，防止 Inspector 旧序列化值导致透出残影
            bgColor = new Color(0.06f, 0.06f, 0.06f, 1f);
            sectionBgColor = new Color(0.1f, 0.1f, 0.1f, 0.95f);
        }

        void OnEnable()
        {
            EventBus.Subscribe<DeviceOpenedEvent>(OnDeviceOpened);
            EventBus.Subscribe<DeviceClosedEvent>(OnDeviceClosed);
            InputRouter.BindKey(KeyCode.Escape, InputPriority.UI, HandleEsc, this);
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<DeviceOpenedEvent>(OnDeviceOpened);
            EventBus.Unsubscribe<DeviceClosedEvent>(OnDeviceClosed);
            InputRouter.UnbindAll(this);
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        bool HandleEsc()
        {
            if (!_isVisible) return false;
            Close();
            return true;
        }

        void OnDeviceOpened(DeviceOpenedEvent evt)
        {
            CloseOtherUIs();
            _currentDevice = evt.Device;
            _deviceData = evt.Device.Data;
            if (_playerInv == null)
                _playerInv = Object.FindObjectOfType<Inventory.Inventory>();
            _selectedRecipeIdx = -1;
            _isVisible = true;
            ScanNearbyDevices();
        }

        void ScanNearbyDevices()
        {
            if (_currentDevice == null) return;
            var all = Object.FindObjectsOfType<ProductionDevice>();
            var list = new System.Collections.Generic.List<ProductionDevice>();
            Vector3 pos = _currentDevice.transform.position;
            foreach (var d in all)
            {
                if (d == _currentDevice || d.Data == null) continue;
                if (Vector3.Distance(pos, d.transform.position) < 15f)
                    list.Add(d);
            }
            _nearbyDevices = list.ToArray();
        }

        void OnDeviceClosed(DeviceClosedEvent evt)
        {
            _isVisible = false;
            _currentDevice = null;
            _deviceData = null;
        }

        void Close()
        {
            _isVisible = false;
            _currentDevice = null;
            _deviceData = null;
            EventBus.Publish(new DeviceClosedEvent());
        }

        void CloseOtherUIs()
        {
            var invUI = Object.FindObjectOfType<_Game.UI.InventoryUI>();
            if (invUI != null)
            {
                if (invUI.overviewPanel != null) invUI.overviewPanel.SetActive(false);
                if (invUI.quickPanel != null) invUI.quickPanel.SetActive(false);
                invUI.SendMessage("SetOtherUIVisible", true, SendMessageOptions.DontRequireReceiver);
            }

            var bmc = Object.FindObjectOfType<_Game.Systems.Building.BuildModeController>();
            if (bmc != null && bmc.IsBuildMode)
                bmc.ForceExit();

            var containerWin = Object.FindObjectOfType<_Game.Systems.WorldContainer.ContainerWindowUI>();
            if (containerWin != null)
                containerWin.CloseWindow();

            // 关闭旧工作台UI，避免透出老UI残影
            var craftingUI = Object.FindObjectOfType<CraftingUI>();
            if (craftingUI != null) craftingUI.Close();

            var researchUI = Object.FindObjectOfType<ChemicalResearchUI>();
            if (researchUI != null) researchUI.Close();

            // 关闭电力/终端面板（同在屏幕正中，会叠影）
            _Game.UI.PowerSourceUI.Hide();
            _Game.UI.TerminalUI.Hide();
        }

        void OnGUI()
        {
            if (!_isVisible || _currentDevice == null || _deviceData == null) return;
            InitStyles();

            Rect panelRect = new Rect(
                (Screen.width - panelWidth) * 0.5f,
                (Screen.height - panelHeight) * 0.5f,
                panelWidth, panelHeight);

            // 全不透明黑底（覆盖老UI残留），用 Box 裁剪
            GUI.color = bgColor;
            GUI.Box(panelRect, "", _bgBoxStyle);
            GUI.color = Color.white;

            // BeginGroup 裁剪所有内容，防止越界绘制
            GUI.BeginGroup(panelRect);
            try
            {
                Rect localRect = new Rect(0, 0, panelWidth, panelHeight);

                DrawHeaderLocal(localRect);
                DrawRecipePanelLocal(localRect);
                DrawStatusPanelLocal(localRect);
            }
            finally
            {
                GUI.EndGroup();
            }
        }

        void InitStyles()
        {
            if (_stylesReady) return;
            _stylesReady = true;

            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = textColor }
            };

            _sectionHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = textColor }
            };

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13, alignment = TextAnchor.MiddleLeft,
                normal = { textColor = textColor },
                wordWrap = true, padding = new RectOffset(4, 4, 2, 2)
            };

            _dimStyle = new GUIStyle(_labelStyle)
            {
                fontSize = 12,
                normal = { textColor = dimTextColor }
            };

            _btnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white },
                padding = new RectOffset(6, 6, 4, 4)
            };

            _disabledBtnStyle = new GUIStyle(_btnStyle)
            {
                normal = { textColor = dimTextColor }
            };

            _greenBtnStyle = new GUIStyle(_btnStyle)
            {
                fontSize = 12
            };

            _closeBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = textColor }
            };

            _bgBoxStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = Texture2D.whiteTexture },
                border = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
                padding = new RectOffset(0, 0, 0, 0)
            };
        }

        void DrawHeaderLocal(Rect panel)
        {
            // panel 已是局部坐标 (0,0,w,h)
            Rect headerRect = new Rect(padding, padding,
                panel.width - padding * 2, 30f);
            GUI.Label(headerRect, $"  {_deviceData.deviceName}", _headerStyle);

            Rect closeRect = new Rect(panel.width - 60f, padding, 48f, 28f);
            if (GUI.Button(closeRect, "✕", _closeBtnStyle))
                Close();

            Rect lineRect = new Rect(padding, headerRect.y + headerRect.height + 2f,
                panel.width - padding * 2, 2f);
            GUI.color = new Color(0.3f, 0.3f, 0.3f, 1f);
            GUI.DrawTexture(lineRect, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        // ---- 左侧：配方列表 ----

        void DrawRecipePanelLocal(Rect panel)
        {
            if (_deviceData == null) return;

            float x = padding;
            float y = padding + 48f;
            float h = panel.height - padding * 2 - 52f;

            Rect bg = new Rect(x, y, leftWidth, h);
            GUI.color = sectionBgColor;
            GUI.DrawTexture(bg, Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.Label(new Rect(x + 6f, y + 4f, leftWidth - 12f, 20f), "生产配方", _sectionHeaderStyle);
            y += 24f;

            // 研究状态检查
            var researchMgr = Object.FindObjectOfType<ChemicalResearchManager>();
            bool researched = researchMgr == null || researchMgr.IsDeviceUnlocked(_deviceData.deviceName);
            if (!researched)
            {
                GUI.Label(new Rect(x + 8f, y, leftWidth - 16f, 40f),
                    "未研究\n请在研究中心完成对应研究项目以解锁此设备配方", _dimStyle);
                return;
            }

            var recipes = _deviceData.recipes;
            if (recipes == null || recipes.Length == 0)
            {
                GUI.Label(new Rect(x + 8f, y, leftWidth - 16f, 20f), "无配方", _dimStyle);
                return;
            }

            float recipeBtnH = buttonHeight + 2f; // 配方按钮稍高
            float totalH = recipes.Length * (recipeBtnH + 6f);
            float viewH = h - 30f;
            _recipeScrollPos = GUI.BeginScrollView(
                new Rect(x, y, leftWidth, viewH),
                _recipeScrollPos,
                new Rect(0, 0, leftWidth - 20f, totalH));

            for (int i = 0; i < recipes.Length; i++)
            {
                var r = recipes[i];
                bool selected = _selectedRecipeIdx == i;

                // 配方级研究门控
                bool recipeLocked = false;
                if (!string.IsNullOrEmpty(r.recipeId) && researchMgr != null)
                    recipeLocked = !researchMgr.IsRecipeUnlocked(r.recipeId);

                Rect btnRect = new Rect(2f, i * (recipeBtnH + 6f), leftWidth - 24f, recipeBtnH);

                Color prev = GUI.backgroundColor;
                if (recipeLocked)
                    GUI.backgroundColor = new Color(0.12f, 0.12f, 0.12f, 0.7f);
                else
                    GUI.backgroundColor = selected ? new Color(0f, 0.45f, 0f, 1f) : new Color(0.18f, 0.18f, 0.18f, 1f);

                string prefix = recipeLocked ? "🔒 " : "";
                string label;
                if (r.inputs != null && r.inputs.Length > 0)
                {
                    var parts = new System.Collections.Generic.List<string>();
                    foreach (var req in r.inputs)
                        parts.Add($"{ItemName(req.itemData)}×{req.count}");
                    label = $"{prefix}{string.Join("+", parts)} → {ItemName(r.output)}×{r.outputCount}";
                }
                else
                {
                    label = $"{prefix}{ItemName(r.input)}×{r.inputCount} → {ItemName(r.output)}×{r.outputCount}";
                }

                GUIStyle btnStyle = recipeLocked ? _dimStyle : _labelStyle;
                if (GUI.Button(btnRect, label, btnStyle))
                {
                    if (!recipeLocked)
                        _selectedRecipeIdx = i;
                }
                GUI.backgroundColor = prev;
            }

            GUI.EndScrollView();
        }

        // ---- 右侧：设备状态 ----

        void DrawStatusPanelLocal(Rect panel)
        {
            if (_deviceData == null) return;

            float x = padding + leftWidth + 8f;
            float y = padding + 48f;
            float h = panel.height - padding * 2 - 52f;

            Rect bg = new Rect(x, y, rightWidth, h);
            GUI.color = sectionBgColor;
            GUI.DrawTexture(bg, Texture2D.whiteTexture);
            GUI.color = Color.white;

            float curY = y + 6f;
            float contentBottom = y + h - 6f; // 可用区域底部

            // ====== 上半区：状态 + 产出 ======

            // 电力/煤模式状态
            var consumer = _currentDevice?.GetComponent<Power.PowerConsumer>();
            if (consumer != null)
            {
                string statusText;
                Color statusColor;
                if (_currentDevice.IsElectricPowered)
                {
                    statusText = $"⚡ 电网供电 ({consumer.requiredPower}W)";
                    statusColor = Color.green;
                }
                else if (_currentDevice.IsCoalPowered)
                {
                    statusText = "🔥 烧煤运转";
                    statusColor = new Color(1f, 0.7f, 0.2f);
                }
                else
                {
                    statusText = consumer.allowCoal ? "⏳ 等待供电/加煤" : "❌ 无电力 - 设备停摆";
                    statusColor = new Color(1f, 0.35f, 0.35f);
                }
                GUI.color = statusColor;
                GUI.Label(new Rect(x + 6f, curY, rightWidth - 12f, 18f), statusText, _sectionHeaderStyle);
                GUI.color = Color.white;
                curY += 18f;

                if (_currentDevice.IsElectricPowered)
                {
                    GUI.Label(new Rect(x + 6f, curY, rightWidth - 12f, 14f),
                        $"速度倍率: ×{consumer.electricSpeedMultiplier}", _dimStyle);
                    curY += 14f;
                }
                curY += 2f;
            }

            // 燃料状态（非通电模式下显示）
            if (!_currentDevice.IsElectricPowered && _deviceData.requiresFuel)
            {
                GUI.Label(new Rect(x + 6f, curY, rightWidth - 12f, 18f),
                    $"燃料: {_currentDevice.FuelRemaining:F0} 轮  {(_currentDevice.FuelRemaining > 0 ? "运行中" : "待加注")}",
                    _currentDevice.FuelRemaining > 5f ? _labelStyle : _dimStyle);
                curY += 18f;

                float fuelPct = Mathf.Clamp01(_currentDevice.FuelRemaining / (_deviceData.fuelPerCycle * 30f));
                Rect fuelBarBg = new Rect(x + 6f, curY, rightWidth - 14f, 8f);
                GUI.color = new Color(0.2f, 0.2f, 0.2f, 1f);
                GUI.DrawTexture(fuelBarBg, Texture2D.whiteTexture);
                GUI.color = fuelColor;
                GUI.DrawTexture(new Rect(x + 6f, curY, (rightWidth - 14f) * fuelPct, 8f), Texture2D.whiteTexture);
                GUI.color = Color.white;
                curY += 12f;
            }

            // 流水线链接（紧凑型）
            DrawLinkSectionCompact(x, ref curY, rightWidth);

            curY += 2f;

            // 产出物品
            GUI.Label(new Rect(x + 6f, curY, rightWidth - 12f, 18f), "┃ 产出物品", _sectionHeaderStyle);
            curY += 20f;

            var outSlot = _currentDevice.OutputSlot;
            if (outSlot != null && outSlot.placedItems != null && outSlot.placedItems.Count > 0)
            {
                float outViewH = 80f; // 精简高度
                float outTotalH = outSlot.placedItems.Count * (buttonHeight + 3f);
                _outputScrollPos = GUI.BeginScrollView(
                    new Rect(x + 2f, curY, rightWidth - 6f, Mathf.Min(outViewH, outTotalH + 6f)),
                    _outputScrollPos,
                    new Rect(0, 0, rightWidth - 24f, outTotalH));

                float itemY = 0f;
                var itemsCopy = new List<_Game.Systems.Inventory.PlacedItem>(outSlot.placedItems);
                foreach (var pi in itemsCopy)
                {
                    if (pi.itemData == null) continue;
                    Rect itemRect = new Rect(2f, itemY, rightWidth - 28f, buttonHeight);
                    GUI.Label(new Rect(itemRect.x, itemRect.y, itemRect.width - 68f, itemRect.height),
                        $"{pi.itemData.itemName} ×{pi.count}", _labelStyle);

                    Rect takeBtn = new Rect(itemRect.x + itemRect.width - 64f, itemRect.y + 1f, 60f, 24f);
                    GUI.backgroundColor = btnColor;
                    if (GUI.Button(takeBtn, "取出", _btnStyle))
                    {
                        if (_playerInv != null)
                        {
                            int added = _playerInv.AddItem(pi.itemData, pi.count);
                            if (added > 0)
                                outSlot.RemoveItem(pi.itemData, added);
                        }
                    }
                    GUI.backgroundColor = Color.white;

                    itemY += buttonHeight + 3f;
                }

                GUI.EndScrollView();
                curY += Mathf.Min(outViewH, outSlot.placedItems.Count * (buttonHeight + 3f)) + 2f;

                // 取出全部
                Rect takeAllBtn = new Rect(x + 6f, curY, 90f, 22f);
                GUI.backgroundColor = btnColor;
                if (GUI.Button(takeAllBtn, "取出全部", _btnStyle))
                    TakeAllOutput();
                GUI.backgroundColor = Color.white;
                curY += 26f;
            }
            else
            {
                GUI.Label(new Rect(x + 8f, curY, rightWidth - 16f, 16f), "(空)", _dimStyle);
                curY += 20f;
            }

            // ====== 分隔线 ======
            curY += 4f;
            GUI.color = new Color(0.35f, 0.35f, 0.35f, 0.6f);
            GUI.DrawTexture(new Rect(x + 8f, curY, rightWidth - 18f, 1f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            curY += 6f;

            // ====== 下半区：输入材料 + 提交按钮 ======

            GUI.Label(new Rect(x + 6f, curY, rightWidth - 12f, 18f), "┃ 输入材料", _sectionHeaderStyle);
            curY += 20f;

            var inSlot = _currentDevice.InputSlot;
            if (inSlot != null && inSlot.placedItems != null && inSlot.placedItems.Count > 0)
            {
                float inViewH = 50f; // 紧凑高度
                float inTotalH = inSlot.placedItems.Count * (buttonHeight + 2f);
                _inputScrollPos = GUI.BeginScrollView(
                    new Rect(x + 2f, curY, rightWidth - 6f, Mathf.Min(inViewH, inTotalH + 4f)),
                    _inputScrollPos,
                    new Rect(0, 0, rightWidth - 24f, inTotalH));

                float itemY = 0f;
                var inItems = new List<_Game.Systems.Inventory.PlacedItem>(inSlot.placedItems);
                foreach (var pi in inItems)
                {
                    if (pi.itemData == null) continue;
                    GUI.Label(new Rect(2f, itemY, rightWidth - 28f, 22f),
                        $"{pi.itemData.itemName} ×{pi.count}", _dimStyle);
                    itemY += 24f;
                }

                GUI.EndScrollView();
                curY += Mathf.Min(inViewH, inSlot.placedItems.Count * 24f) + 4f;
            }
            else
            {
                GUI.Label(new Rect(x + 8f, curY, rightWidth - 16f, 16f), "(空)", _dimStyle);
                curY += 20f;
            }

            curY += 4f;

            // ====== 补充材料按钮（自然流布局，不钉死底部） ======
            if (_selectedRecipeIdx >= 0 && _deviceData.recipes != null &&
                _selectedRecipeIdx < _deviceData.recipes.Length)
            {
                var selRecipe = _deviceData.recipes[_selectedRecipeIdx];
                bool isMulti = selRecipe.inputs != null && selRecipe.inputs.Length > 0;

                if (isMulti)
                {
                    var reqs = selRecipe.inputs;
                    // 如果按钮会超出面板底部，则从底部往上排
                    float totalBtnH = reqs.Length * 32f;
                    float btnStartY = curY + 2f;
                    float btnEndY = btnStartY + totalBtnH;
                    if (btnEndY > contentBottom)
                        btnStartY = Mathf.Max(y + 6f + 80f, contentBottom - totalBtnH);

                    for (int ri = 0; ri < reqs.Length; ri++)
                    {
                        var req = reqs[ri];
                        if (req.itemData == null) continue;
                        int canSupply = _playerInv != null ? _playerInv.GetItemCount(req.itemData) : 0;
                        bool canFeed = canSupply >= req.count;

                        float btnY = btnStartY + ri * 32f;
                        Rect supplyBtn = new Rect(x + 6f, btnY, rightWidth - 14f, 28f);
                        GUI.enabled = canFeed;
                        GUI.backgroundColor = canFeed ? btnColor : new Color(0.3f, 0.3f, 0.3f, 0.6f);
                        string supplyText = canFeed
                            ? $"▼ 补充 {ItemName(req.itemData)}×{req.count}  (拥有:{canSupply})"
                            : $"✗ 不足 {ItemName(req.itemData)}×{req.count}  (拥有:{canSupply})";
                        if (GUI.Button(supplyBtn, supplyText, _btnStyle))
                            SupplyInput(req.itemData, req.count);
                        GUI.backgroundColor = Color.white;
                        GUI.enabled = true;
                    }
                }
                else
                {
                    int canSupply = _playerInv != null ? _playerInv.GetItemCount(selRecipe.input) : 0;
                    bool canFeed = canSupply >= selRecipe.inputCount;

                    // 自然流，若贴底则上移
                    float btnY = curY + 4f;
                    if (btnY + 32f > contentBottom)
                        btnY = contentBottom - 32f;

                    Rect supplyBtn = new Rect(x + 6f, btnY, rightWidth - 14f, 30f);
                    GUI.enabled = canFeed;
                    GUI.backgroundColor = canFeed ? btnColor : new Color(0.3f, 0.3f, 0.3f, 0.6f);
                    string supplyText = canFeed
                        ? $"▼ 补充材料 ({ItemName(selRecipe.input)}×{selRecipe.inputCount})  (拥有:{canSupply})"
                        : $"✗ 材料不足 ({ItemName(selRecipe.input)}×{selRecipe.inputCount})  (拥有:{canSupply})";
                    if (GUI.Button(supplyBtn, supplyText, _btnStyle))
                        SupplyInput(selRecipe.input, selRecipe.inputCount);
                    GUI.backgroundColor = Color.white;
                    GUI.enabled = true;
                }
            }
        }

        /// <summary>紧凑型链接区</summary>
        void DrawLinkSectionCompact(float x, ref float curY, float width)
        {
            if (_currentDevice == null) return;

            var dest = _currentDevice.OutputDestination;
            if (dest != null)
            {
                string destName = dest.Data != null ? dest.Data.deviceName : "???";
                GUI.Label(new Rect(x + 6f, curY, width - 80f, 18f),
                    $"→ 链接: {destName}", _dimStyle);

                Rect unlinkBtn = new Rect(x + width - 68f, curY, 62f, 20f);
                GUI.backgroundColor = new Color(0.7f, 0.15f, 0.15f, 1f);
                if (GUI.Button(unlinkBtn, "断开", _btnStyle))
                    _currentDevice.OutputDestination = null;
                GUI.backgroundColor = Color.white;
                curY += 22f;
            }
            else if (_nearbyDevices != null && _nearbyDevices.Length > 0)
            {
                // 只显示一行可链接设备提示
                int linkable = 0;
                foreach (var d in _nearbyDevices)
                    if (d != null && d.Data != null && CanLinkTo(d)) linkable++;

                if (linkable > 0)
                {
                    GUI.Label(new Rect(x + 6f, curY, width - 12f, 18f),
                        $"可链接 {linkable} 个设备", _dimStyle);
                    curY += 18f;
                }
            }
        }

        // ---- 辅助方法 ----

        void TakeAllOutput()
        {
            var outSlot = _currentDevice?.OutputSlot;
            if (outSlot == null || _playerInv == null) return;

            var items = new List<_Game.Systems.Inventory.PlacedItem>(outSlot.placedItems);
            foreach (var pi in items)
            {
                if (pi.itemData == null) continue;
                int added = _playerInv.AddItem(pi.itemData, pi.count);
                if (added > 0)
                    outSlot.RemoveItem(pi.itemData, added);
            }
        }

        void SupplyInput(ItemData item, int count)
        {
            if (_playerInv == null || _currentDevice == null || item == null) return;

            var inSlot = _currentDevice.InputSlot;
            if (inSlot == null) return;

            int toTransfer = Mathf.Min(count, _playerInv.GetItemCount(item));
            if (toTransfer <= 0) return;

            int added = inSlot.AddItem(item, toTransfer, float.MaxValue);
            if (added > 0)
                _playerInv.RemoveItem(item, added);
        }

        /// <summary>当前设备的产出能否被目标设备作为原料接收</summary>
        bool CanLinkTo(ProductionDevice target)
        {
            if (_deviceData?.recipes == null || target?.Data?.recipes == null) return false;
            foreach (var myRecipe in _deviceData.recipes)
            {
                if (myRecipe.output == null) continue;
                foreach (var theirRecipe in target.Data.recipes)
                {
                    if (theirRecipe.input == myRecipe.output)
                        return true;
                }
            }
            return false;
        }

        static string ItemName(ItemData item)
        {
            return item != null ? item.itemName : "???";
        }

        // ---- 流水线链接 ----

        void DrawLinkSection(float x, ref float curY, float width)
        {
            if (_currentDevice == null) return;

            GUI.Label(new Rect(x + 6f, curY, width - 12f, 20f), "输出链接", _sectionHeaderStyle);
            curY += 22f;

            var dest = _currentDevice.OutputDestination;
            if (dest != null)
            {
                string destName = dest.Data != null ? dest.Data.deviceName : "???";
                GUI.Label(new Rect(x + 6f, curY, width - 80f, 22f),
                    $"→ {destName}", _labelStyle);

                Rect unlinkBtn = new Rect(x + width - 70f, curY, 64f, 22f);
                GUI.backgroundColor = new Color(0.7f, 0.15f, 0.15f, 1f);
                if (GUI.Button(unlinkBtn, "断开", _btnStyle))
                    _currentDevice.OutputDestination = null;
                GUI.backgroundColor = Color.white;
                curY += 26f;
            }
            else
            {
                GUI.Label(new Rect(x + 6f, curY, width - 12f, 18f), "(无链接)", _dimStyle);
                curY += 20f;

                if (_nearbyDevices != null && _nearbyDevices.Length > 0)
                {
                    float viewH = Mathf.Min(60f, _nearbyDevices.Length * 24f);
                    float totalH = _nearbyDevices.Length * 24f;
                    _linkScrollPos = GUI.BeginScrollView(
                        new Rect(x + 2f, curY, width - 6f, viewH),
                        _linkScrollPos,
                        new Rect(0, 0, width - 24f, totalH));

                    float itemY = 0f;
                    foreach (var d in _nearbyDevices)
                    {
                        if (d == null || d.Data == null) continue;
                        if (!CanLinkTo(d)) continue; // 只显示能接收当前产出的设备
                        string devName = d.Data.deviceName;
                        Rect linkBtn = new Rect(2f, itemY, width - 28f, 22f);
                        GUI.backgroundColor = new Color(0.15f, 0.45f, 0.15f, 1f);
                        if (GUI.Button(linkBtn, $"链接 → {devName}", _btnStyle))
                            _currentDevice.OutputDestination = d;
                        GUI.backgroundColor = Color.white;
                        itemY += 24f;
                    }

                    GUI.EndScrollView();
                    curY += viewH + 4f;
                }
                else
                {
                    GUI.Label(new Rect(x + 6f, curY, width - 12f, 18f), "(附近无可用设备)", _dimStyle);
                    curY += 20f;
                }
            }
        }
    }
}
