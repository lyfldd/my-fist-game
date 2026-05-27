using UnityEngine;
using _Game.Config;
using _Game.Core;

namespace _Game.Systems.AIBot
{
    /// <summary>
    /// AI机器人主面板 + 各设置浮动窗。主面板自定义拖动，浮动窗使用 GUI.Window 原生拖拽。
    /// </summary>
    struct FloatWindow
    {
        public bool visible;
        public Rect rect;
    }

    public class AIBotUI : MonoBehaviour
    {
        static AIBotUI _instance;
        AIBot _bot;
        AIBotCombat _combat;

        bool _visible;
        Rect _panelRect;
        Vector2 _scrollPos;

        // 主面板拖动
        bool _isDragging;
        Vector2 _dragOffset;
        float _panelX = -1f, _panelY = -1f;

        // 浮动窗口（各自独立拖动）
        FloatWindow _wFollow, _wGuard, _wPatrol, _wRightArm, _wLeftArm, _wReactor;
        Vector2 _followScrollPos, _guardScrollPos, _patrolScrollPos, _rightArmConfigScroll, _leftArmConfigScroll;
        ItemData _equippedRightArmItem;
        ItemData _equippedLeftArmItem;
        System.Collections.Generic.Dictionary<string, ItemData> _knownAmmoItems
            = new System.Collections.Generic.Dictionary<string, ItemData>();

        // 样式
        GUIStyle _headerStyle, _normalStyle, _greenStyle, _redStyle, _yellowStyle, _btnStyle, _smallBtnStyle;
        GUIStyle _floatWindowStyle;
        Texture2D _winBg;
        bool _stylesInit;

        // GUI.Window 唯一 ID
        const int WID_FOLLOW    = 901;
        const int WID_GUARD     = 902;
        const int WID_PATROL    = 903;
        const int WID_RIGHT_ARM = 904;
        const int WID_LEFT_ARM  = 905;
        const int WID_REACTOR   = 906;

        // ============================================================
        // 静态接口
        // ============================================================

        public static void Show(AIBot bot)
        {
            if (_instance == null)
            {
                var go = new GameObject("AIBotUI");
                _instance = go.AddComponent<AIBotUI>();
            }
            _instance._bot = bot;
            _instance._combat = bot?.GetComponent<AIBotCombat>();
            _instance._visible = true;
            _instance._panelX = -1f;
            _instance._wFollow = default; _instance._wGuard = default;
            _instance._wPatrol = default; _instance._wRightArm = default;
            _instance._wLeftArm = default; _instance._wReactor = default;
        }

        public static void Hide()
        {
            if (_instance != null)
                _instance._visible = false;
        }

        public static bool IsVisible => _instance != null && _instance._visible;

        // ============================================================
        // OnGUI
        // ============================================================

        void OnGUI()
        {
            if (!_visible || _bot == null) return;
            InitStyles();

            // 面板尺寸
            float maxPanelH = Screen.height - 40f;
            float w = 420f, h = Mathf.Min(420f, maxPanelH);

            // 首次显示时居中
            if (_panelX < 0f) { _panelX = (Screen.width - w) * 0.5f; _panelY = (Screen.height - h) * 0.5f; }

            // 限制不拖出屏幕
            _panelX = Mathf.Clamp(_panelX, -w + 60f, Screen.width - 60f);
            _panelY = Mathf.Clamp(_panelY, 0f, Screen.height - 40f);
            _panelRect = new Rect(_panelX, _panelY, w, h);

            // 拖动处理
            HandleDrag();

            // 背景
            GUI.color = new Color(0.05f, 0.05f, 0.08f, 0.93f);
            GUI.DrawTexture(_panelRect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUILayout.BeginArea(_panelRect);

            // 标题（可拖动区域）
            GUILayout.Space(8);
            Rect titleRect = GUILayoutUtility.GetRect(w - 16f, 24f);
            GUI.Label(titleRect, "AI机器人 (拖拽标题移动)", _headerStyle);

            // 关闭按钮
            Rect closeRect = new Rect(w - 32f, 10f, 24f, 24f);
            GUI.color = new Color(1f, 0.3f, 0.3f);
            if (GUI.Button(closeRect, "✕", _smallBtnStyle))
                Hide();
            GUI.color = Color.white;

            GUILayout.Space(4);

            // 滚动区域
            float scrollH = h - 50f;
            _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Height(scrollH));

            DrawHPBar();
            GUILayout.Space(8);

            DrawEnergySection();
            GUILayout.Space(8);

            DrawPrioritySection();
            GUILayout.Space(4);

            DrawAlertRangeSlider();
            GUILayout.Space(8);

            DrawWeaponSlots();
            GUILayout.Space(8);

            DrawCommandButtons();
            GUILayout.Space(8);

            DrawActionButtons();
            GUILayout.Space(12);

            GUILayout.EndScrollView();
            GUILayout.EndArea();

            // 设置浮动窗（各自使用 GUI.Window 独立拖动）
            if (_wFollow.visible) DrawFollowSettings();
            if (_wGuard.visible) DrawGuardSettings();
            if (_wPatrol.visible) DrawPatrolSettings();
            if (_wRightArm.visible) DrawRightArmConfig();
            if (_wLeftArm.visible) DrawLeftArmConfig();
            if (_wReactor.visible) DrawReactorUI();

            // 点面板外关闭
            if (Event.current.type == EventType.MouseDown &&
                !_panelRect.Contains(Event.current.mousePosition) &&
                !IsClickInSettings(Event.current.mousePosition))
                Hide();
        }

        bool IsClickInSettings(Vector2 pos)
        {
            if (_wFollow.visible && _wFollow.rect.Contains(pos)) return true;
            if (_wGuard.visible && _wGuard.rect.Contains(pos)) return true;
            if (_wPatrol.visible && _wPatrol.rect.Contains(pos)) return true;
            if (_wRightArm.visible && _wRightArm.rect.Contains(pos)) return true;
            if (_wLeftArm.visible && _wLeftArm.rect.Contains(pos)) return true;
            if (_wReactor.visible && _wReactor.rect.Contains(pos)) return true;
            return false;
        }

        // ============================================================
        // 血量条
        // ============================================================

        void DrawHPBar()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("❤", _normalStyle, GUILayout.Width(24));
            float pct = _bot.HealthPercent;
            Color barColor = pct > 0.5f ? Color.green : (pct > 0.25f ? Color.yellow : Color.red);

            Rect barRect = GUILayoutUtility.GetRect(300f, 20f);
            GUI.color = new Color(0.3f, 0.3f, 0.3f);
            GUI.DrawTexture(barRect, Texture2D.whiteTexture);
            GUI.color = barColor;
            GUI.DrawTexture(new Rect(barRect.x, barRect.y, barRect.width * pct, barRect.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUILayout.Label($"{_bot.HP:F0}/{_bot.MaxHP:F0}", _normalStyle, GUILayout.Width(80));
            GUILayout.EndHorizontal();

            if (_bot.IsShieldAvailable || _bot.shieldActive)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("🛡", _normalStyle, GUILayout.Width(24));
                float spct = _bot.ShieldPercent;
                Color sColor = _bot.IsShieldActive && _bot.shieldStartupTimer <= 0f
                    ? new Color(0.3f, 0.5f, 1f) : Color.gray;

                Rect sBarRect = GUILayoutUtility.GetRect(300f, 14f);
                GUI.color = new Color(0.2f, 0.2f, 0.3f);
                GUI.DrawTexture(sBarRect, Texture2D.whiteTexture);
                GUI.color = sColor;
                GUI.DrawTexture(new Rect(sBarRect.x, sBarRect.y, sBarRect.width * spct, sBarRect.height), Texture2D.whiteTexture);
                GUI.color = Color.white;

                if (_bot.shieldStartupTimer > 0f)
                    GUILayout.Label($"启动中... {_bot.shieldStartupTimer:F1}s", _yellowStyle, GUILayout.Width(100));
                else if (!_bot.shieldActive)
                    GUILayout.Label("未开启", _smallBtnStyle, GUILayout.Width(60));
                else
                    GUILayout.Label($"{_bot.ShieldCurrentHP:F0}/{_bot.ShieldMaxHP:F0}", _normalStyle, GUILayout.Width(80));

                GUILayout.EndHorizontal();

                if (_bot.IsShieldAvailable)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(28);
                    if (_bot.shieldActive)
                    {
                        if (GUILayout.Button("关闭能量盾", _smallBtnStyle, GUILayout.Width(100)))
                            _bot.DeactivateShield();
                    }
                    else
                    {
                        GUI.enabled = _bot.UraniumCurrent >= AIBot.SHIELD_STARTUP_COST;
                        if (GUILayout.Button("开启能量盾 (30铀)", _smallBtnStyle, GUILayout.Width(130)))
                            _bot.ActivateShield();
                        GUI.enabled = true;
                    }
                    GUILayout.Label(_bot.shieldActive && _bot.shieldCurrentHP < _bot.shieldMaxHP
                        ? $"维持:{AIBot.SHIELD_MAINTENANCE_RECHARGE}铀/s 回复:{AIBot.SHIELD_REGEN_PER_SEC}盾/s"
                        : $"维持:{AIBot.SHIELD_MAINTENANCE_FULL}铀/s", _smallBtnStyle);
                    GUILayout.EndHorizontal();
                }
            }

            if (_bot.IsLowHP)
            {
                GUI.color = Color.red;
                GUILayout.Label("⚠ 重伤！", _redStyle);
                GUI.color = Color.white;
            }
        }

        // ============================================================
        // 能量区域
        // ============================================================

        void DrawEnergySection()
        {
            var mode = _bot.CurrentEnergyMode;

            string modeLabel = mode switch
            {
                EnergyMode.EnergySaving => "♻ 节能模式",
                EnergyMode.Electric => "● 电力模式",
                EnergyMode.Uranium => "☢ 铀模式",
                EnergyMode.Burst => "💥 爆发模式",
                _ => "? 未知"
            };
            Color modeColor = mode switch
            {
                EnergyMode.EnergySaving => Color.green,
                EnergyMode.Electric => Color.cyan,
                EnergyMode.Uranium => new Color(1f, 0.6f, 0f),
                EnergyMode.Burst => new Color(1f, 0.3f, 0f),
                _ => Color.white
            };
            GUI.color = modeColor;
            GUILayout.Label($"■ {modeLabel} (当前)", _normalStyle);
            GUI.color = Color.white;

            GUILayout.BeginHorizontal();
            GUI.color = Color.gray;
            GUILayout.Label($"消耗×{_bot.ConsumptionMultiplier:F2}  速度×{_bot.SpeedMultiplier:F2}  冷却×{_bot.CooldownMultiplier:F2}", _smallBtnStyle);
            GUI.color = Color.white;
            GUILayout.EndHorizontal();

            GUILayout.Space(2);

            DrawEnergyBar("🔋 电池", _bot.BatteryPercent, _bot.BatteryCurrent, _bot.BatteryMax, new Color(0.2f, 0.7f, 1f));
            DrawEnergyBar("☢ 铀燃料", _bot.UraniumPercent, _bot.UraniumCurrent, _bot.UraniumMax, new Color(1f, 0.5f, 0f));

            GUILayout.BeginHorizontal();
            GUI.backgroundColor = _bot.ecoModeEnabled ? Color.green : Color.gray;
            if (GUILayout.Button(_bot.ecoModeEnabled ? "节能:开" : "节能:关", _btnStyle, GUILayout.Height(28)))
                _bot.ToggleEcoMode();
            GUI.backgroundColor = Color.white;

            bool canBurst = _bot.UraniumCurrent > 0f || (_bot.ecoModeEnabled && _bot.BatteryCurrent > 0f);
            GUI.backgroundColor = _bot.burstModeEnabled ? new Color(1f, 0.3f, 0f) : Color.gray;
            if (GUILayout.Button(_bot.burstModeEnabled ? "爆发:开" : "爆发:关", _btnStyle, GUILayout.Height(28)))
                _bot.ToggleBurstMode();
            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();

            if (_bot.ecoModeEnabled)
            {
                GUI.color = Color.green;
                GUILayout.Label("⚠ 节能中：激光/AI辅助/AI接管已禁用", _smallBtnStyle);
                GUI.color = Color.white;
            }

            GUILayout.Space(2);

            GUILayout.BeginHorizontal();
            bool solarActive = _bot.IsSolarActive;
            GUI.color = solarActive ? Color.green : Color.gray;
            GUILayout.Label(solarActive ? "☀ 太阳能板 (工作中)" : "☀ 太阳能板 (休眠)", _normalStyle);
            GUI.color = Color.white;
            if (solarActive)
                GUILayout.Label($"+{_bot.CurrentSolarRate:F1}/h", _greenStyle);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            bool nuclearActive = _bot.IsNuclearActive;
            GUI.color = nuclearActive ? new Color(1f, 0.6f, 0f) : Color.gray;
            GUILayout.Label(nuclearActive ? "⚛ 微型反应堆 (运行中)" : "⚛ 微型反应堆 (休眠)", _normalStyle);
            GUI.color = Color.white;
            if (nuclearActive)
                GUILayout.Label($"+{_bot.CurrentNuclearRate:F1}/h", _yellowStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("打开反应堆", _smallBtnStyle, GUILayout.Width(90)))
            { _wReactor.visible = !_wReactor.visible; if (_wReactor.visible) _wReactor.rect = new Rect(0, 0, 0, 0); }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("移动速度:", _normalStyle, GUILayout.Width(80));
            float sliderVal = GUILayout.HorizontalSlider(_bot.speedSliderValue, 0.25f, 1f, GUILayout.Width(200f));
            _bot.speedSliderValue = Mathf.Round(sliderVal * 100f) / 100f;
            float actualSpeed = (_bot.IsPiloted ? _bot.pilotBaseSpeed : _bot.moveSpeed) * _bot.SpeedMultiplier * _bot.speedSliderValue;
            GUILayout.Label($"{actualSpeed:F1} m/s", _normalStyle, GUILayout.Width(60));
            GUILayout.EndHorizontal();

            if (_bot.IsShutdown)
            {
                GUI.color = Color.red;
                GUILayout.Label("⚠ 能量耗尽，已停机！", _redStyle);
                GUI.color = Color.white;
            }
        }

        void DrawEnergyBar(string label, float percent, float current, float max, Color fillColor)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, _normalStyle, GUILayout.Width(80));
            Rect barRect = GUILayoutUtility.GetRect(200f, 16f);
            GUI.color = new Color(0.3f, 0.3f, 0.3f);
            GUI.DrawTexture(barRect, Texture2D.whiteTexture);
            GUI.color = fillColor;
            GUI.DrawTexture(new Rect(barRect.x, barRect.y, barRect.width * percent, barRect.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUILayout.Label($"{current:F1}/{max:F0}", _normalStyle, GUILayout.Width(80));
            GUILayout.EndHorizontal();
        }

        // ============================================================
        // 攻击优先级
        // ============================================================

        void DrawPrioritySection()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("攻击优先级:", _normalStyle, GUILayout.Width(80));

            if (_combat != null)
            {
                string p1 = PriorityName(_combat.slot1);
                string p2 = PriorityName(_combat.slot2);
                string p3 = PriorityName(_combat.slot3);
                GUILayout.Label($"[{p1}] → [{p2}] → [{p3}]", _normalStyle);

                if (GUILayout.Button("切换", _smallBtnStyle, GUILayout.Width(50)))
                    _combat.CyclePriority();
            }
            GUILayout.EndHorizontal();
        }

        string PriorityName(AttackPriority p)
        {
            switch (p)
            {
                case AttackPriority.Laser: return "激光";
                case AttackPriority.RightArm: return "右臂";
                case AttackPriority.LeftArm: return "左臂";
                default: return "?";
            }
        }

        // ============================================================
        // 警觉距离滑块
        // ============================================================

        void DrawAlertRangeSlider()
        {
            if (_combat == null) return;

            GUILayout.BeginHorizontal();
            GUILayout.Label("警觉距离:", _normalStyle, GUILayout.Width(80));
            float val = GUILayout.HorizontalSlider(_combat.alertRange, _combat.alertRangeMin, _combat.alertRangeMax,
                GUILayout.Width(200f));
            _combat.alertRange = Mathf.Round(val);
            GUILayout.Label($"{_combat.alertRange:F0}m", _normalStyle, GUILayout.Width(50));
            GUILayout.Label($"{_combat.alertRangeMin:F0}m~{_combat.alertRangeMax:F0}m", _smallBtnStyle, GUILayout.Width(80));
            GUILayout.EndHorizontal();
        }

        // ============================================================
        // 武器挂载显示
        // ============================================================

        void DrawWeaponSlots()
        {
            if (_combat == null) return;

            GUILayout.BeginHorizontal();

            // 右臂
            GUILayout.BeginVertical(GUILayout.Width(190));
            GUILayout.BeginHorizontal();
            GUILayout.Label("┌ 右臂", _smallBtnStyle);
            if (GUILayout.Button("⚙", _smallBtnStyle, GUILayout.Width(28), GUILayout.Height(18)))
            { _wRightArm.visible = !_wRightArm.visible; if (_wRightArm.visible) _wRightArm.rect = new Rect(0, 0, 0, 0); }
            GUILayout.Label("──────────┐", _smallBtnStyle);
            GUILayout.EndHorizontal();
            string rightName = _combat.CurrentRightArm switch
            {
                RightArmWeapon.None => "空",
                RightArmWeapon.Pistol => "手枪",
                RightArmWeapon.Rifle => "步枪",
                RightArmWeapon.Shotgun => "霰弹枪",
                RightArmWeapon.ElectromagneticRifle => "电磁步枪",
                _ => "?"
            };
            GUILayout.Label($"  {rightName}", _normalStyle);
            GUILayout.Label("└────────────────┘", _smallBtnStyle);
            GUILayout.EndVertical();

            GUILayout.Space(10);

            // 左臂
            GUILayout.BeginVertical(GUILayout.Width(190));
            GUILayout.BeginHorizontal();
            GUILayout.Label("┌ 左臂", _smallBtnStyle);
            if (GUILayout.Button("⚙", _smallBtnStyle, GUILayout.Width(28), GUILayout.Height(18)))
            { _wLeftArm.visible = !_wLeftArm.visible; if (_wLeftArm.visible) _wLeftArm.rect = new Rect(0, 0, 0, 0); }
            GUILayout.Label("──────────┐", _smallBtnStyle);
            GUILayout.EndHorizontal();
            string leftName = _combat.CurrentLeftArm switch
            {
                LeftArmWeapon.None => "空",
                LeftArmWeapon.Shield => "盾牌 (受伤-30%)",
                LeftArmWeapon.Chainsaw => "电锯 (15/s)",
                LeftArmWeapon.Knife => "短刀 (20/1.5s)",
                _ => "?"
            };
            GUILayout.Label($"  {leftName}", _normalStyle);
            GUILayout.Label("└────────────────┘", _smallBtnStyle);
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
        }

        // ============================================================
        // 指令按钮
        // ============================================================

        void DrawCommandButtons()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("指令:", _normalStyle, GUILayout.Width(40));

            bool isFollow = _bot.CurrentCommand == AIBotCommand.Follow;
            bool isGuard = _bot.CurrentCommand == AIBotCommand.Guard;
            bool isPatrol = _bot.CurrentCommand == AIBotCommand.Patrol;

            GUI.backgroundColor = isFollow ? Color.green : Color.gray;
            if (GUILayout.Button("跟随", GUILayout.Width(70), GUILayout.Height(32)))
                _bot.SetCommand(AIBotCommand.Follow);

            GUI.backgroundColor = Color.white;
            if (GUILayout.Button("⚙", _smallBtnStyle, GUILayout.Width(28), GUILayout.Height(32)))
            { _wFollow.visible = !_wFollow.visible; if (_wFollow.visible) _wFollow.rect = new Rect(0, 0, 0, 0); }
            GUILayout.Space(4);

            GUI.backgroundColor = isGuard ? Color.green : Color.gray;
            if (GUILayout.Button("驻守", GUILayout.Width(70), GUILayout.Height(32)))
                _bot.SetCommand(AIBotCommand.Guard);

            GUI.backgroundColor = Color.white;
            if (GUILayout.Button("⚙", _smallBtnStyle, GUILayout.Width(28), GUILayout.Height(32)))
            { _wGuard.visible = !_wGuard.visible; if (_wGuard.visible) _wGuard.rect = new Rect(0, 0, 0, 0); }
            GUILayout.Space(4);

            GUI.backgroundColor = isPatrol ? Color.green : Color.gray;
            if (GUILayout.Button("巡逻", GUILayout.Width(70), GUILayout.Height(32)))
                _bot.SetCommand(AIBotCommand.Patrol);

            GUI.backgroundColor = Color.white;
            if (GUILayout.Button("⚙", _smallBtnStyle, GUILayout.Width(28), GUILayout.Height(32)))
            { _wPatrol.visible = !_wPatrol.visible; if (_wPatrol.visible) _wPatrol.rect = new Rect(0, 0, 0, 0); }

            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();
        }

        // ============================================================
        // 操作按钮
        // ============================================================

        void DrawActionButtons()
        {
            GUILayout.BeginHorizontal();

            if (GUILayout.Button("打开背包", _btnStyle, GUILayout.Height(34)))
            {
                var botInventory = _bot.GetComponent<AIBotInventory>();
                if (botInventory != null)
                    AIBotInventoryUI.Show(_bot, botInventory);
            }

            if (GUILayout.Button("加燃料", _btnStyle, GUILayout.Height(34)))
            {
                AddFuelFromPlayerInventory();
            }

            if (GUILayout.Button("修理", _btnStyle, GUILayout.Height(34)))
            {
                RepairFromPlayerInventory();
            }

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();

            var pilot = _bot.GetComponent<AIBotPilot>();
            if (pilot != null)
            {
                bool isPiloting = pilot.IsPiloting;
                GUI.backgroundColor = isPiloting ? new Color(1f, 0.5f, 0f) : Color.gray;
                if (GUILayout.Button(isPiloting ? "退出驾驶" : "驾驶", _btnStyle, GUILayout.Height(34)))
                {
                    if (isPiloting)
                        pilot.ExitPilot();
                    else
                        pilot.EnterPilot(FindObjectOfType<_Game.Systems.Character.PlayerCharacter>()?.gameObject);
                }
                GUI.backgroundColor = Color.white;
            }
            else
            {
                GUI.enabled = false;
                GUILayout.Button("驾驶(缺组件)", _btnStyle, GUILayout.Height(34));
                GUI.enabled = true;
            }

            if (_combat != null)
            {
                bool canOverride = _bot.IsAIWeaponOverrideEnabled;
                if (!canOverride) _combat.aiWeaponOverride = false;

                bool aiOverride = _combat.aiWeaponOverride;
                GUI.enabled = canOverride;
                GUI.backgroundColor = aiOverride ? Color.green : Color.gray;
                if (GUILayout.Button(aiOverride ? "AI接管:开" : "AI接管:关", _btnStyle, GUILayout.Height(34)))
                    _combat.aiWeaponOverride = !_combat.aiWeaponOverride;
                GUI.backgroundColor = Color.white;
                GUI.enabled = true;
            }

            GUILayout.EndHorizontal();
        }

        // ============================================================
        // 加燃料 / 修理
        // ============================================================

        void AddFuelFromPlayerInventory()
        {
            var inv = FindObjectOfType<_Game.Systems.Inventory.Inventory>();
            if (inv == null) { Debug.LogWarning("[AIBotUI] 未找到玩家背包"); return; }

            foreach (var placed in inv.placedItems)
            {
                if (placed.itemData != null && placed.itemData.itemName == "电池组" && placed.count > 0)
                {
                    int toConsume = Mathf.Min(placed.count, 5);
                    for (int i = 0; i < toConsume; i++)
                    {
                        inv.RemoveItem(placed.itemData, 1);
                        _bot.AddBatteryEnergy(40f);
                    }
                    Debug.Log($"[AIBotUI] 消耗电池组 x{toConsume}，电池能量 +{toConsume * 40}");
                    return;
                }
            }

            foreach (var placed in inv.placedItems)
            {
                if (placed.itemData != null && placed.itemData.itemName == "浓缩铀" && placed.count > 0)
                {
                    inv.RemoveItem(placed.itemData, 1);
                    _bot.AddUraniumFuel();
                    Debug.Log("[AIBotUI] 消耗浓缩铀 x1，铀燃料已满");
                    return;
                }
            }

            Debug.LogWarning("[AIBotUI] 背包内无电池组或浓缩铀");
        }

        void RepairFromPlayerInventory()
        {
            var inv = FindObjectOfType<_Game.Systems.Inventory.Inventory>();
            if (inv == null) { Debug.LogWarning("[AIBotUI] 未找到玩家背包"); return; }

            foreach (var placed in inv.placedItems)
            {
                if (placed.itemData != null && placed.itemData.itemName == "高级零件" && placed.count > 0)
                {
                    inv.RemoveItem(placed.itemData, 1);
                    _bot.RepairHP(50f);
                    Debug.Log($"[AIBotUI] 消耗高级零件 x1，HP +50 → {_bot.HP:F0}/{_bot.MaxHP}");
                    return;
                }
            }

            foreach (var placed in inv.placedItems)
            {
                if (placed.itemData != null && placed.itemData.itemName == "铁锭" && placed.count > 0)
                {
                    inv.RemoveItem(placed.itemData, 1);
                    _bot.RepairHP(25f);
                    Debug.Log($"[AIBotUI] 消耗铁锭 x1，HP +25 → {_bot.HP:F0}/{_bot.MaxHP}");
                    return;
                }
            }

            Debug.LogWarning("[AIBotUI] 背包内无铁锭或高级零件");
        }

        // ============================================================
        // 右臂武器配置浮动窗
        // ============================================================

        void DrawRightArmConfig()
        {
            float fw = 280f, fh = 320f;
            InitFloatPos(ref _wRightArm, fw, fh, false);

            _wRightArm.rect = GUI.Window(WID_RIGHT_ARM, _wRightArm.rect, RightArmWindowFunc,
                "右臂武器管理", _floatWindowStyle);
        }

        void RightArmWindowFunc(int id)
        {
            var currentWeapon = _combat.CurrentRightArm;
            string ammoName = AIBotCombat.GetAmmoNameForWeapon(currentWeapon);
            var inv = FindObjectOfType<_Game.Systems.Inventory.Inventory>();

            _rightArmConfigScroll = GUILayout.BeginScrollView(_rightArmConfigScroll, GUILayout.Height(240f));

            GUILayout.BeginHorizontal();
            GUILayout.Label($"当前: {WeaponDisplayName(currentWeapon)}", _normalStyle);
            if (!string.IsNullOrEmpty(ammoName))
                GUILayout.Label($"弹药: {AIBotCombat.GetAmmoDisplayName(ammoName)}", _smallBtnStyle);
            GUILayout.EndHorizontal();

            if (currentWeapon != RightArmWeapon.None)
            {
                if (GUILayout.Button("卸下", _smallBtnStyle, GUILayout.Width(60)))
                    TryUnequipRightArm();
            }

            GUILayout.Space(6);
            GUILayout.Label("── 可装备武器 ──", _smallBtnStyle);

            if (inv != null)
            {
                foreach (RightArmWeapon rw in System.Enum.GetValues(typeof(RightArmWeapon)))
                {
                    if (rw == RightArmWeapon.None) continue;
                    string itemName = AIBotCombat.GetWeaponItemName(rw);
                    string ammoForWeapon = AIBotCombat.GetAmmoNameForWeapon(rw);
                    int count = CountPlayerItem(inv, itemName);

                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"{WeaponDisplayName(rw)} ×{count}\n需: {AIBotCombat.GetAmmoDisplayName(ammoForWeapon)}", _smallBtnStyle, GUILayout.Width(130));

                    if (currentWeapon == rw)
                    {
                        GUI.color = Color.green;
                        GUILayout.Label("已装备", _smallBtnStyle);
                        GUI.color = Color.white;
                    }
                    else if (count > 0)
                    {
                        if (GUILayout.Button("装备", _smallBtnStyle, GUILayout.Width(50)))
                        {
                            var itemData = FindPlayerItem(inv, itemName);
                            if (itemData != null)
                                TryEquipRightArm(rw, itemData);
                        }
                    }
                    else
                    {
                        GUILayout.Label("—", _smallBtnStyle);
                    }
                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.Space(6);

            if (!string.IsNullOrEmpty(ammoName))
            {
                GUILayout.Label("── 弹药 ──", _smallBtnStyle);
                int botAmmo = _combat.GetAmmoCount(ammoName);
                int playerAmmo = inv != null ? CountPlayerItem(inv, ammoName) : 0;
                GUILayout.Label($"机器人: {botAmmo}发 | 玩家: {playerAmmo}发", _normalStyle);

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("装入全部", _smallBtnStyle, GUILayout.Width(80)))
                    LoadAmmoToBot(ammoName);
                if (GUILayout.Button("取出全部", _smallBtnStyle, GUILayout.Width(80)))
                    UnloadAmmoFromBot(ammoName);
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(8);
            if (GUILayout.Button("关闭", _btnStyle, GUILayout.Height(28)))
                _wRightArm.visible = false;

            GUILayout.EndScrollView();
            GUI.DragWindow(new Rect(0, 0, 10000, 20));
        }

        // ============================================================
        // 左臂武器配置浮动窗
        // ============================================================

        void DrawLeftArmConfig()
        {
            float fw = 260f, fh = 280f;
            InitFloatPos(ref _wLeftArm, fw, fh, false);

            _wLeftArm.rect = GUI.Window(WID_LEFT_ARM, _wLeftArm.rect, LeftArmWindowFunc,
                "左臂武器管理", _floatWindowStyle);
        }

        void LeftArmWindowFunc(int id)
        {
            var currentWeapon = _combat.CurrentLeftArm;
            var inv = FindObjectOfType<_Game.Systems.Inventory.Inventory>();

            _leftArmConfigScroll = GUILayout.BeginScrollView(_leftArmConfigScroll, GUILayout.Height(200f));

            GUILayout.Label($"当前: {WeaponDisplayName(currentWeapon)}", _normalStyle);

            if (currentWeapon != LeftArmWeapon.None)
            {
                if (GUILayout.Button("卸下", _smallBtnStyle, GUILayout.Width(60)))
                    TryUnequipLeftArm();
            }

            GUILayout.Space(6);
            GUILayout.Label("── 可装备武器 ──", _smallBtnStyle);

            if (inv != null)
            {
                foreach (LeftArmWeapon lw in System.Enum.GetValues(typeof(LeftArmWeapon)))
                {
                    if (lw == LeftArmWeapon.None) continue;
                    string itemName = AIBotCombat.GetWeaponItemName(lw);
                    int count = CountPlayerItem(inv, itemName);

                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"{WeaponDisplayName(lw)} ×{count}", _normalStyle, GUILayout.Width(140));

                    if (currentWeapon == lw)
                    {
                        GUI.color = Color.green;
                        GUILayout.Label("已装备", _smallBtnStyle);
                        GUI.color = Color.white;
                    }
                    else if (count > 0)
                    {
                        if (GUILayout.Button("装备", _smallBtnStyle, GUILayout.Width(50)))
                        {
                            var itemData = FindPlayerItem(inv, itemName);
                            if (itemData != null)
                                TryEquipLeftArm(lw, itemData);
                        }
                    }
                    else
                    {
                        GUILayout.Label("—", _smallBtnStyle);
                    }
                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.Space(8);
            if (GUILayout.Button("关闭", _btnStyle, GUILayout.Height(28)))
                _wLeftArm.visible = false;

            GUILayout.EndScrollView();
            GUI.DragWindow(new Rect(0, 0, 10000, 20));
        }

        // ============================================================
        // 武器/弹药操作
        // ============================================================

        void TryEquipRightArm(RightArmWeapon weapon, ItemData itemData)
        {
            var inv = FindObjectOfType<_Game.Systems.Inventory.Inventory>();
            if (inv == null) return;

            if (_combat.CurrentRightArm != RightArmWeapon.None)
                TryUnequipRightArm();

            inv.RemoveItem(itemData, 1);
            _combat.EquipRightArm(weapon);
            _equippedRightArmItem = itemData;

            Debug.Log($"[AIBotUI] 装备右臂武器: {WeaponDisplayName(weapon)}");

            string ammoName = AIBotCombat.GetAmmoNameForWeapon(weapon);
            if (!string.IsNullOrEmpty(ammoName))
                LoadAmmoToBot(ammoName);
        }

        void TryUnequipRightArm()
        {
            var weapon = _combat.CurrentRightArm;
            if (weapon == RightArmWeapon.None) return;

            var inv = FindObjectOfType<_Game.Systems.Inventory.Inventory>();
            if (inv == null) return;

            if (_equippedRightArmItem != null)
            {
                inv.AddItem(_equippedRightArmItem, 1);
                _equippedRightArmItem = null;
            }

            string ammoName = AIBotCombat.GetAmmoNameForWeapon(weapon);
            if (!string.IsNullOrEmpty(ammoName))
                UnloadAmmoFromBot(ammoName);

            _combat.UnequipRightArm();
            Debug.Log($"[AIBotUI] 卸下右臂武器: {WeaponDisplayName(weapon)}");
        }

        void TryEquipLeftArm(LeftArmWeapon weapon, ItemData itemData)
        {
            var inv = FindObjectOfType<_Game.Systems.Inventory.Inventory>();
            if (inv == null) return;

            if (_combat.CurrentLeftArm != LeftArmWeapon.None)
                TryUnequipLeftArm();

            inv.RemoveItem(itemData, 1);
            _combat.EquipLeftArm(weapon);
            _equippedLeftArmItem = itemData;

            Debug.Log($"[AIBotUI] 装备左臂武器: {WeaponDisplayName(weapon)}");
        }

        void TryUnequipLeftArm()
        {
            var weapon = _combat.CurrentLeftArm;
            if (weapon == LeftArmWeapon.None) return;

            var inv = FindObjectOfType<_Game.Systems.Inventory.Inventory>();
            if (inv == null) return;

            if (_equippedLeftArmItem != null)
            {
                inv.AddItem(_equippedLeftArmItem, 1);
                _equippedLeftArmItem = null;
            }

            _combat.UnequipLeftArm();
            Debug.Log($"[AIBotUI] 卸下左臂武器: {WeaponDisplayName(weapon)}");
        }

        void LoadAmmoToBot(string ammoName)
        {
            var inv = FindObjectOfType<_Game.Systems.Inventory.Inventory>();
            if (inv == null) return;

            var itemData = FindPlayerItem(inv, ammoName);
            if (itemData == null) { Debug.LogWarning($"[AIBotUI] 背包内无 {ammoName}"); return; }

            int playerCount = CountPlayerItem(inv, ammoName);
            if (playerCount <= 0) return;

            if (!_knownAmmoItems.ContainsKey(ammoName))
                _knownAmmoItems[ammoName] = itemData;

            int loaded = _combat.LoadAmmo(ammoName, playerCount);
            inv.RemoveItem(itemData, loaded);
            Debug.Log($"[AIBotUI] 装入弹药: {ammoName} ×{loaded}");
        }

        void UnloadAmmoFromBot(string ammoName)
        {
            var inv = FindObjectOfType<_Game.Systems.Inventory.Inventory>();
            if (inv == null) return;

            int botCount = _combat.GetAmmoCount(ammoName);
            if (botCount <= 0) return;

            var itemData = FindPlayerItem(inv, ammoName);
            if (itemData == null)
                _knownAmmoItems.TryGetValue(ammoName, out itemData);
            if (itemData == null)
            {
                Debug.LogWarning($"[AIBotUI] 无法找到 {ammoName} 的 ItemData，无法取出");
                return;
            }

            int unloaded = _combat.UnloadAmmo(ammoName, botCount);
            inv.AddItem(itemData, unloaded);
            Debug.Log($"[AIBotUI] 取出弹药: {ammoName} ×{unloaded}");
        }

        // ============================================================
        // 背包扫描工具
        // ============================================================

        int CountPlayerItem(_Game.Systems.Inventory.Inventory inv, string itemName)
        {
            int total = 0;
            foreach (var placed in inv.placedItems)
            {
                if (placed.itemData != null && placed.itemData.itemName == itemName)
                    total += placed.count;
            }
            return total;
        }

        ItemData FindPlayerItem(_Game.Systems.Inventory.Inventory inv, string itemName)
        {
            foreach (var placed in inv.placedItems)
            {
                if (placed.itemData != null && placed.itemData.itemName == itemName && placed.count > 0)
                    return placed.itemData;
            }
            return null;
        }

        string WeaponDisplayName(RightArmWeapon w)
        {
            switch (w)
            {
                case RightArmWeapon.None: return "空";
                case RightArmWeapon.Pistol: return "手枪";
                case RightArmWeapon.Rifle: return "步枪";
                case RightArmWeapon.Shotgun: return "霰弹枪";
                case RightArmWeapon.ElectromagneticRifle: return "电磁步枪";
                default: return "?";
            }
        }

        string WeaponDisplayName(LeftArmWeapon w)
        {
            switch (w)
            {
                case LeftArmWeapon.None: return "空";
                case LeftArmWeapon.Shield: return "盾牌";
                case LeftArmWeapon.Chainsaw: return "电锯";
                case LeftArmWeapon.Knife: return "短刀";
                default: return "?";
            }
        }

        // ============================================================
        // 跟随设置浮动窗
        // ============================================================

        void DrawFollowSettings()
        {
            float fw = 220f, fh = 200f;
            InitFloatPos(ref _wFollow, fw, fh, false);

            _wFollow.rect = GUI.Window(WID_FOLLOW, _wFollow.rect, FollowWindowFunc,
                "跟随设置", _floatWindowStyle);
        }

        void FollowWindowFunc(int id)
        {
            _followScrollPos = GUILayout.BeginScrollView(_followScrollPos, GUILayout.Height(110f));

            GUILayout.Label($"距离: {_bot.followDistance:F0}m", _normalStyle);
            float val = GUILayout.HorizontalSlider(_bot.followDistance, _bot.followDistanceMin, _bot.followDistanceMax,
                GUILayout.Width(190f));
            _bot.followDistance = Mathf.Round(val);
            GUILayout.Label($"{_bot.followDistanceMin:F0}m ~ {_bot.followDistanceMax:F0}m", _smallBtnStyle);

            GUILayout.Space(8);

            GUILayout.Label("快捷预设:", _normalStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("紧贴(1m)", _smallBtnStyle))
                _bot.followDistance = 1f;
            if (GUILayout.Button("适中(3m)", _smallBtnStyle))
                _bot.followDistance = 3f;
            if (GUILayout.Button("松散(8m)", _smallBtnStyle))
                _bot.followDistance = 8f;
            GUILayout.EndHorizontal();

            GUILayout.Space(8);
            if (GUILayout.Button("确定", _btnStyle, GUILayout.Height(28)))
                _wFollow.visible = false;

            GUILayout.EndScrollView();
            GUI.DragWindow(new Rect(0, 0, 10000, 20));
        }

        // ============================================================
        // 驻守设置浮动窗
        // ============================================================

        void DrawGuardSettings()
        {
            float fw = 240f, fh = 200f;
            InitFloatPos(ref _wGuard, fw, fh, false);

            _wGuard.rect = GUI.Window(WID_GUARD, _wGuard.rect, GuardWindowFunc,
                "驻守设置", _floatWindowStyle);
        }

        void GuardWindowFunc(int id)
        {
            _guardScrollPos = GUILayout.BeginScrollView(_guardScrollPos, GUILayout.Height(100f));

            _bot.guardAutoRecallEnabled = GUILayout.Toggle(_bot.guardAutoRecallEnabled, "超距自动解除");

            GUI.enabled = _bot.guardAutoRecallEnabled;
            GUILayout.Label($"解除距离: {_bot.guardAutoRecallDistance:F0}m", _normalStyle);
            float val = GUILayout.HorizontalSlider(_bot.guardAutoRecallDistance, 50f, 300f,
                GUILayout.Width(190f));
            _bot.guardAutoRecallDistance = Mathf.Round(val);
            GUILayout.Label("50m ~ 300m", _smallBtnStyle);
            GUI.enabled = true;

            GUILayout.Space(8);
            if (GUILayout.Button("确定", _btnStyle, GUILayout.Height(28)))
                _wGuard.visible = false;

            GUILayout.EndScrollView();
            GUI.DragWindow(new Rect(0, 0, 10000, 20));
        }

        // ============================================================
        // 巡逻设置浮动窗
        // ============================================================

        void DrawPatrolSettings()
        {
            float fw = 260f, fh = 280f;
            InitFloatPos(ref _wPatrol, fw, fh, false);

            _wPatrol.rect = GUI.Window(WID_PATROL, _wPatrol.rect, PatrolWindowFunc,
                "巡逻设置", _floatWindowStyle);
        }

        void PatrolWindowFunc(int id)
        {
            _patrolScrollPos = GUILayout.BeginScrollView(_patrolScrollPos, GUILayout.Height(180f));

            bool aroundPlayer = GUILayout.Toggle(_bot.patrolAroundPlayer, "围绕玩家");
            if (aroundPlayer != _bot.patrolAroundPlayer)
            {
                _bot.patrolAroundPlayer = aroundPlayer;
                if (!aroundPlayer)
                    _bot.patrolCenterPoint = _bot.transform.position;
            }

            if (!_bot.patrolAroundPlayer)
            {
                GUILayout.Label("● 指定地点", _yellowStyle);
                if (GUILayout.Button("设为当前位置", _smallBtnStyle, GUILayout.Height(24)))
                    _bot.patrolCenterPoint = _bot.transform.position;
                GUILayout.Label($"  坐标: {_bot.patrolCenterPoint:F0}", _smallBtnStyle);
            }
            else
            {
                GUILayout.Label("● 围绕玩家", _greenStyle);
            }

            GUILayout.Space(4);

            GUILayout.Label($"巡逻半径: {_bot.patrolRadius:F0}m", _normalStyle);
            float r = GUILayout.HorizontalSlider(_bot.patrolRadius, _bot.patrolRadiusMin, _bot.patrolRadiusMax,
                GUILayout.Width(200f));
            _bot.patrolRadius = Mathf.Round(r);
            GUILayout.Label($"{_bot.patrolRadiusMin:F0}m ~ {_bot.patrolRadiusMax:F0}m", _smallBtnStyle);

            GUILayout.Space(4);

            _bot.patrolAutoRecallEnabled = GUILayout.Toggle(_bot.patrolAutoRecallEnabled, "超距自动解除");

            GUI.enabled = _bot.patrolAutoRecallEnabled;
            GUILayout.Label($"解除距离: {_bot.patrolAutoRecallDistance:F0}m", _normalStyle);
            float d = GUILayout.HorizontalSlider(_bot.patrolAutoRecallDistance, 100f, 500f,
                GUILayout.Width(200f));
            _bot.patrolAutoRecallDistance = Mathf.Round(d);
            GUILayout.Label("100m ~ 500m", _smallBtnStyle);
            GUI.enabled = true;

            GUILayout.Space(8);
            if (GUILayout.Button("确定", _btnStyle, GUILayout.Height(28)))
                _wPatrol.visible = false;

            GUILayout.EndScrollView();
            GUI.DragWindow(new Rect(0, 0, 10000, 20));
        }

        // ============================================================
        // 微型反应堆窗口 (3×3)
        // ============================================================

        void DrawReactorUI()
        {
            float fw = 260f, fh = 320f;
            InitFloatPos(ref _wReactor, fw, fh, true);

            _wReactor.rect = GUI.Window(WID_REACTOR, _wReactor.rect, ReactorWindowFunc,
                "⚛ 微型反应堆 (3×3)", _floatWindowStyle);
        }

        void ReactorWindowFunc(int id)
        {
            GUILayout.Label(_bot.IsNuclearActive
                ? $"运行中: +{_bot.CurrentNuclearRate:F1} 铀/h"
                : "休眠中 (非铀模式)", _normalStyle);
            GUILayout.Space(4);

            for (int row = 0; row < 3; row++)
            {
                GUILayout.BeginHorizontal();
                for (int col = 0; col < 3; col++)
                {
                    int idx = row * 3 + col;
                    var slot = _bot.reactorSlots[idx];

                    GUILayout.BeginVertical(GUILayout.Width(70), GUILayout.Height(70));

                    Rect cellRect = GUILayoutUtility.GetRect(64f, 64f);
                    if (slot.IsEmpty)
                    {
                        GUI.color = new Color(0.2f, 0.2f, 0.25f);
                        GUI.DrawTexture(cellRect, Texture2D.whiteTexture);
                        GUI.color = Color.white;
                        GUI.Label(new Rect(cellRect.x + 15, cellRect.y + 20, 40f, 20f), "空", _smallBtnStyle);
                    }
                    else
                    {
                        Color cellColor = slot.IsLarge ? new Color(1f, 0.4f, 0f, 0.6f) : new Color(0.2f, 0.7f, 1f, 0.5f);
                        GUI.color = cellColor;
                        GUI.DrawTexture(cellRect, Texture2D.whiteTexture);
                        GUI.color = Color.white;
                        GUI.Label(new Rect(cellRect.x + 4, cellRect.y + 4, 56f, 16f),
                            slot.IsLarge ? "大核心" : "小核心", _smallBtnStyle);
                        float remainPct = slot.burnRemaining / Mathf.Max(slot.burnTime, 0.01f);
                        GUI.Label(new Rect(cellRect.x + 4, cellRect.y + 20, 56f, 16f),
                            $"{slot.burnRemaining:F1}h", _yellowStyle);
                        GUI.Label(new Rect(cellRect.x + 4, cellRect.y + 36, 56f, 16f),
                            $"{slot.outputRate:F0}/h", _greenStyle);

                        GUI.color = new Color(0.3f, 0.3f, 0.3f);
                        GUI.DrawTexture(new Rect(cellRect.x + 2, cellRect.y + 54, 60f, 8f), Texture2D.whiteTexture);
                        GUI.color = slot.IsLarge ? new Color(1f, 0.5f, 0f) : Color.cyan;
                        GUI.DrawTexture(new Rect(cellRect.x + 2, cellRect.y + 54, 60f * remainPct, 8f), Texture2D.whiteTexture);
                        GUI.color = Color.white;
                    }

                    if (slot.IsEmpty)
                    {
                        if (GUILayout.Button("装入", _smallBtnStyle, GUILayout.Height(20)))
                            LoadFusionCore(idx);
                    }
                    else
                    {
                        if (GUILayout.Button("取出", _smallBtnStyle, GUILayout.Height(20)))
                        {
                            ReturnFusionCore(idx);
                            _bot.reactorSlots[idx] = default;
                        }
                    }

                    GUILayout.EndVertical();
                }
                GUILayout.EndHorizontal();
                GUILayout.Space(2);
            }

            GUILayout.Space(8);
            if (GUILayout.Button("关闭", _btnStyle, GUILayout.Height(28)))
                _wReactor.visible = false;

            GUI.DragWindow(new Rect(0, 0, 10000, 20));
        }

        void LoadFusionCore(int slotIdx)
        {
            var inv = FindObjectOfType<_Game.Systems.Inventory.Inventory>();
            if (inv == null) { Debug.LogWarning("[ReactorUI] 未找到玩家背包"); return; }

            foreach (var placed in inv.placedItems)
            {
                if (placed.itemData == null || placed.count <= 0) continue;
                bool isSmall = placed.itemData.itemName == "聚变核心(小)";
                bool isLarge = placed.itemData.itemName == "聚变核心(大)";
                if (!isSmall && !isLarge) continue;

                var itemRef = placed.itemData;
                inv.RemoveItem(placed.itemData, 1);

                _bot.reactorSlots[slotIdx] = new FusionCoreSlot
                {
                    itemName = itemRef.itemName,
                    itemData = itemRef,
                    burnTime = isLarge ? 12f : 4f,
                    burnRemaining = isLarge ? 12f : 4f,
                    outputRate = isLarge ? 45f : 30f
                };
                Debug.Log($"[ReactorUI] 装入 {itemRef.itemName} 到槽位 {slotIdx}");
                return;
            }

            Debug.LogWarning("[ReactorUI] 背包中没有聚变核心");
        }

        void ReturnFusionCore(int slotIdx)
        {
            var inv = FindObjectOfType<_Game.Systems.Inventory.Inventory>();
            if (inv == null) return;

            var slot = _bot.reactorSlots[slotIdx];
            if (slot.IsEmpty) return;

            if (slot.itemData != null)
                inv.AddItem(slot.itemData, 1);

            _bot.reactorSlots[slotIdx] = default;
            Debug.Log($"[ReactorUI] 取出 {slot.itemName} 从槽位 {slotIdx}");
        }

        // ============================================================
        // 浮动窗位置初始化
        // ============================================================

        void InitFloatPos(ref FloatWindow w, float width, float height, bool leftSide)
        {
            if (w.rect.width < 1f)
            {
                float x = leftSide
                    ? _panelRect.x - width - 10f
                    : _panelRect.x + _panelRect.width + 10f;
                float y = _panelRect.y + 40f;
                x = Mathf.Clamp(x, -width + 60f, Screen.width - 60f);
                y = Mathf.Clamp(y, 0f, Screen.height - 40f);
                w.rect = new Rect(x, y, width, height);
            }
        }

        // ============================================================
        // 主面板拖动
        // ============================================================

        void HandleDrag()
        {
            Event e = Event.current;
            Rect dragArea = new Rect(_panelRect.x, _panelRect.y, _panelRect.width, 40f);

            if (e.type == EventType.MouseDown && e.button == 0 && dragArea.Contains(e.mousePosition))
            {
                Rect closeRect = new Rect(_panelRect.x + _panelRect.width - 32f, _panelRect.y + 10f, 24f, 24f);
                if (!closeRect.Contains(e.mousePosition))
                {
                    _isDragging = true;
                    _dragOffset = e.mousePosition - new Vector2(_panelRect.x, _panelRect.y);
                    e.Use();
                }
            }

            if (e.type == EventType.MouseUp && e.button == 0)
                _isDragging = false;

            if (_isDragging && e.type == EventType.MouseDrag)
            {
                _panelX = e.mousePosition.x - _dragOffset.x;
                _panelY = e.mousePosition.y - _dragOffset.y;
                e.Use();
            }
        }

        // ============================================================
        // 样式初始化
        // ============================================================

        void InitStyles()
        {
            if (_stylesInit) return;
            _stylesInit = true;

            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            _normalStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14, normal = { textColor = Color.white }
            };
            _greenStyle = new GUIStyle(_normalStyle)
            {
                normal = { textColor = Color.green }
            };
            _redStyle = new GUIStyle(_normalStyle)
            {
                normal = { textColor = new Color(1f, 0.4f, 0.4f) }
            };
            _yellowStyle = new GUIStyle(_normalStyle)
            {
                normal = { textColor = Color.yellow }
            };
            _btnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14, fontStyle = FontStyle.Bold
            };
            _smallBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12
            };

            // 浮动窗样式（深色背景，无边框）
            _winBg = new Texture2D(1, 1);
            _winBg.SetPixel(0, 0, new Color(0.08f, 0.08f, 0.14f, 0.95f));
            _winBg.Apply();

            _floatWindowStyle = new GUIStyle(GUI.skin.window);
            _floatWindowStyle.normal.background = _winBg;
            _floatWindowStyle.onNormal.background = _winBg;
            _floatWindowStyle.border = new RectOffset(6, 6, 22, 6);
            _floatWindowStyle.padding = new RectOffset(10, 10, 24, 10);
            _floatWindowStyle.contentOffset = new Vector2(0, -2);
            _floatWindowStyle.normal.textColor = Color.white;
            _floatWindowStyle.fontSize = 14;
            _floatWindowStyle.fontStyle = FontStyle.Bold;
            _floatWindowStyle.alignment = TextAnchor.UpperCenter;
        }
    }
}
