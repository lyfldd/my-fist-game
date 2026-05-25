using UnityEngine;
using _Game.Config;
using _Game.Core;

namespace _Game.Systems.AIBot
{
    /// <summary>
    /// AI机器人主面板 + 模式设置浮动窗。OnGUI 渲染，静态 Show/Hide。
    /// </summary>
    public class AIBotUI : MonoBehaviour
    {
        static AIBotUI _instance;
        AIBot _bot;
        AIBotCombat _combat;

        bool _visible;
        Rect _panelRect;
        Vector2 _scrollPos;

        // 设置窗状态
        bool _showFollowSettings;
        bool _showGuardSettings;
        bool _showPatrolSettings;
        Rect _followSettingsRect;
        Rect _guardSettingsRect;
        Rect _patrolSettingsRect;
        Vector2 _followScrollPos;
        Vector2 _guardScrollPos;
        Vector2 _patrolScrollPos;

        // 武器管理状态
        bool _showRightArmConfig;
        bool _showLeftArmConfig;
        Rect _rightArmConfigRect;
        Rect _leftArmConfigRect;
        Vector2 _rightArmConfigScroll;
        Vector2 _leftArmConfigScroll;
        ItemData _equippedRightArmItem;
        ItemData _equippedLeftArmItem;
        System.Collections.Generic.Dictionary<string, ItemData> _knownAmmoItems
            = new System.Collections.Generic.Dictionary<string, ItemData>();

        // 样式
        GUIStyle _headerStyle, _normalStyle, _greenStyle, _redStyle, _yellowStyle, _btnStyle, _smallBtnStyle;
        bool _stylesInit;

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
            _instance._showFollowSettings = false;
            _instance._showGuardSettings = false;
            _instance._showPatrolSettings = false;
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

            // 面板高度自适应屏幕，内容超出时滚动
            float maxPanelH = Screen.height - 40f;
            float w = 420f, h = Mathf.Min(420f, maxPanelH);
            float x = (Screen.width - w) * 0.5f;
            float y = (Screen.height - h) * 0.5f;
            _panelRect = new Rect(x, y, w, h);

            // 背景
            GUI.color = new Color(0.05f, 0.05f, 0.08f, 0.93f);
            GUI.DrawTexture(_panelRect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUILayout.BeginArea(_panelRect);

            // 标题
            GUILayout.Space(8);
            GUILayout.Label("AI机器人", _headerStyle);
            GUILayout.Space(4);

            // 滚动区域：视口高度 = 面板剩余高度
            float scrollH = h - 50f;
            _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Height(scrollH));

            // === 血量条 ===
            DrawHPBar();
            GUILayout.Space(8);

            // === 能量模式 + 双能量条 ===
            DrawEnergySection();
            GUILayout.Space(8);

            // === 攻击优先级 ===
            DrawPrioritySection();
            GUILayout.Space(4);

            // === 警觉距离 ===
            DrawAlertRangeSlider();
            GUILayout.Space(8);

            // === 武器挂载 ===
            DrawWeaponSlots();
            GUILayout.Space(8);

            // === 指令按钮 ===
            DrawCommandButtons();
            GUILayout.Space(8);

            // === 操作按钮 ===
            DrawActionButtons();
            GUILayout.Space(12);

            GUILayout.EndScrollView();
            GUILayout.EndArea();

            // 设置浮动窗
            if (_showFollowSettings) DrawFollowSettings();
            if (_showGuardSettings) DrawGuardSettings();
            if (_showPatrolSettings) DrawPatrolSettings();

            // 武器管理浮动窗
            if (_showRightArmConfig) DrawRightArmConfig();
            if (_showLeftArmConfig) DrawLeftArmConfig();

            // 点面板外关闭
            if (Event.current.type == EventType.MouseDown &&
                !_panelRect.Contains(Event.current.mousePosition) &&
                !IsClickInSettings(Event.current.mousePosition))
                Hide();
        }

        bool IsClickInSettings(Vector2 pos)
        {
            if (_showFollowSettings && _followSettingsRect.Contains(pos)) return true;
            if (_showGuardSettings && _guardSettingsRect.Contains(pos)) return true;
            if (_showPatrolSettings && _patrolSettingsRect.Contains(pos)) return true;
            if (_showRightArmConfig && _rightArmConfigRect.Contains(pos)) return true;
            if (_showLeftArmConfig && _leftArmConfigRect.Contains(pos)) return true;
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
            bool isBattery = _bot.CurrentEnergyMode == EnergyMode.Battery;

            // 电力模式
            GUI.color = isBattery ? Color.cyan : Color.gray;
            GUILayout.Label(isBattery ? "● 电力模式 (当前)" : "○ 电力模式", _normalStyle);
            GUI.color = Color.white;
            DrawEnergyBar("🔋 电池", _bot.BatteryPercent, _bot.BatteryCurrent, _bot.BatteryMax, new Color(0.2f, 0.7f, 1f));

            if (GUILayout.Button("切换电力模式", _smallBtnStyle, GUILayout.Width(120)))
                _bot.SetEnergyMode(EnergyMode.Battery);

            GUILayout.Space(4);

            // 浓缩铀模式
            GUI.color = !isBattery ? new Color(1f, 0.6f, 0f) : Color.gray;
            GUILayout.Label(!isBattery ? "● 浓缩铀模式 (当前)" : "○ 浓缩铀模式", _normalStyle);
            GUI.color = Color.white;
            DrawEnergyBar("☢ 铀燃料", _bot.UraniumPercent, _bot.UraniumCurrent, _bot.UraniumMax, new Color(1f, 0.5f, 0f));

            if (GUILayout.Button("切换铀模式", _smallBtnStyle, GUILayout.Width(120)))
                _bot.SetEnergyMode(EnergyMode.Uranium);

            GUILayout.Space(4);

            // 太阳能板状态
            GUILayout.BeginHorizontal();
            bool solarActive = _bot.IsSolarActive;
            GUI.color = solarActive ? Color.green : Color.gray;
            GUILayout.Label(solarActive ? "☀ 太阳能板 (工作中)" : "☀ 太阳能板 (休眠)", _normalStyle);
            GUI.color = Color.white;
            if (solarActive)
                GUILayout.Label($"+{_bot.CurrentSolarRate:F1}/h", _greenStyle);
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
                _showRightArmConfig = !_showRightArmConfig;
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
                _showLeftArmConfig = !_showLeftArmConfig;
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
                _showFollowSettings = !_showFollowSettings;
            GUILayout.Space(4);

            GUI.backgroundColor = isGuard ? Color.green : Color.gray;
            if (GUILayout.Button("驻守", GUILayout.Width(70), GUILayout.Height(32)))
                _bot.SetCommand(AIBotCommand.Guard);

            GUI.backgroundColor = Color.white;
            if (GUILayout.Button("⚙", _smallBtnStyle, GUILayout.Width(28), GUILayout.Height(32)))
                _showGuardSettings = !_showGuardSettings;
            GUILayout.Space(4);

            GUI.backgroundColor = isPatrol ? Color.green : Color.gray;
            if (GUILayout.Button("巡逻", GUILayout.Width(70), GUILayout.Height(32)))
                _bot.SetCommand(AIBotCommand.Patrol);

            GUI.backgroundColor = Color.white;
            if (GUILayout.Button("⚙", _smallBtnStyle, GUILayout.Width(28), GUILayout.Height(32)))
                _showPatrolSettings = !_showPatrolSettings;

            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();
        }

        // ============================================================
        // 操作按钮
        // ============================================================

        void DrawActionButtons()
        {
            // 第一行：打开背包 / 加燃料 / 修理
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

            // 第二行：驾驶 / AI接管武器
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
                bool aiOverride = _combat.aiWeaponOverride;
                GUI.backgroundColor = aiOverride ? Color.green : Color.gray;
                if (GUILayout.Button(aiOverride ? "AI接管:开" : "AI接管:关", _btnStyle, GUILayout.Height(34)))
                    _combat.aiWeaponOverride = !_combat.aiWeaponOverride;
                GUI.backgroundColor = Color.white;
            }

            GUILayout.EndHorizontal();
        }

        // ============================================================
        // 加燃料
        // ============================================================

        void AddFuelFromPlayerInventory()
        {
            var inv = FindObjectOfType<_Game.Systems.Inventory.Inventory>();
            if (inv == null) { Debug.LogWarning("[AIBotUI] 未找到玩家背包"); return; }

            // 尝试加电池组
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

            // 尝试加浓缩铀
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

        // ============================================================
        // 修理
        // ============================================================

        void RepairFromPlayerInventory()
        {
            var inv = FindObjectOfType<_Game.Systems.Inventory.Inventory>();
            if (inv == null) { Debug.LogWarning("[AIBotUI] 未找到玩家背包"); return; }

            // 优先用高级零件
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

            // 用铁锭
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
        // 武器管理浮动窗
        // ============================================================

        void DrawRightArmConfig()
        {
            float w = 280f, h = 320f;
            float x = _panelRect.x + _panelRect.width + 10f;
            float y = _panelRect.y;
            _rightArmConfigRect = new Rect(x, y, w, h);

            GUI.color = new Color(0.1f, 0.1f, 0.15f, 0.95f);
            GUI.DrawTexture(_rightArmConfigRect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUILayout.BeginArea(_rightArmConfigRect);
            _rightArmConfigScroll = GUILayout.BeginScrollView(_rightArmConfigScroll, GUILayout.Height(240f));

            GUILayout.Label("右臂武器管理", _headerStyle);
            GUILayout.Space(6);

            var currentWeapon = _combat.CurrentRightArm;
            string ammoName = AIBotCombat.GetAmmoNameForWeapon(currentWeapon);

            // 当前武器
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

            // 可装备武器
            GUILayout.Label("── 可装备武器 ──", _smallBtnStyle);
            var inv = FindObjectOfType<_Game.Systems.Inventory.Inventory>();
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

            // 弹药
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
                _showRightArmConfig = false;

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void DrawLeftArmConfig()
        {
            float w = 260f, h = 280f;
            float x = _panelRect.x + _panelRect.width + 10f;
            float y = _panelRect.y + 160f;
            _leftArmConfigRect = new Rect(x, y, w, h);

            GUI.color = new Color(0.1f, 0.1f, 0.15f, 0.95f);
            GUI.DrawTexture(_leftArmConfigRect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUILayout.BeginArea(_leftArmConfigRect);
            _leftArmConfigScroll = GUILayout.BeginScrollView(_leftArmConfigScroll, GUILayout.Height(200f));

            GUILayout.Label("左臂武器管理", _headerStyle);
            GUILayout.Space(6);

            var currentWeapon = _combat.CurrentLeftArm;

            GUILayout.Label($"当前: {WeaponDisplayName(currentWeapon)}", _normalStyle);

            if (currentWeapon != LeftArmWeapon.None)
            {
                if (GUILayout.Button("卸下", _smallBtnStyle, GUILayout.Width(60)))
                    TryUnequipLeftArm();
            }

            GUILayout.Space(6);

            GUILayout.Label("── 可装备武器 ──", _smallBtnStyle);
            var inv = FindObjectOfType<_Game.Systems.Inventory.Inventory>();
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
                _showLeftArmConfig = false;

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        // ============================================================
        // 武器/弹药操作
        // ============================================================

        void TryEquipRightArm(RightArmWeapon weapon, ItemData itemData)
        {
            var inv = FindObjectOfType<_Game.Systems.Inventory.Inventory>();
            if (inv == null) return;

            // 先卸下当前武器
            if (_combat.CurrentRightArm != RightArmWeapon.None)
                TryUnequipRightArm();

            // 扣除武器物品
            inv.RemoveItem(itemData, 1);
            _combat.EquipRightArm(weapon);
            _equippedRightArmItem = itemData;

            Debug.Log($"[AIBotUI] 装备右臂武器: {WeaponDisplayName(weapon)}");

            // 自动扫描并装入弹药
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

            // 返还武器物品
            if (_equippedRightArmItem != null)
            {
                inv.AddItem(_equippedRightArmItem, 1);
                _equippedRightArmItem = null;
            }

            // 自动取出弹药
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

            // 先卸下当前武器
            if (_combat.CurrentLeftArm != LeftArmWeapon.None)
                TryUnequipLeftArm();

            // 扣除武器物品
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

            // 返还武器物品
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

            // 缓存 ItemData 引用，方便后续取出
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

            // 优先从玩家背包找 ItemData，回退到缓存
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
            float w = 220f, h = 200f;
            float x = _panelRect.x + _panelRect.width + 10f;
            float y = _panelRect.y;
            _followSettingsRect = new Rect(x, y, w, h);

            GUI.color = new Color(0.1f, 0.1f, 0.15f, 0.95f);
            GUI.DrawTexture(_followSettingsRect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUILayout.BeginArea(_followSettingsRect);
            _followScrollPos = GUILayout.BeginScrollView(_followScrollPos, GUILayout.Height(110f));
            GUILayout.Label("跟随设置", _headerStyle);
            GUILayout.Space(6);

            GUILayout.Label($"距离: {_bot.followDistance:F0}m", _normalStyle);
            float val = GUILayout.HorizontalSlider(_bot.followDistance, _bot.followDistanceMin, _bot.followDistanceMax,
                GUILayout.Width(190f));
            _bot.followDistance = Mathf.Round(val);
            GUILayout.Label($"{_bot.followDistanceMin:F0}m ~ {_bot.followDistanceMax:F0}m", _smallBtnStyle);

            GUILayout.Space(8);

            // 快捷预设
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
                _showFollowSettings = false;

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        // ============================================================
        // 驻守设置浮动窗
        // ============================================================

        void DrawGuardSettings()
        {
            float w = 240f, h = 200f;
            float x = _panelRect.x + _panelRect.width + 10f;
            float y = _panelRect.y + 80f;
            _guardSettingsRect = new Rect(x, y, w, h);

            GUI.color = new Color(0.1f, 0.1f, 0.15f, 0.95f);
            GUI.DrawTexture(_guardSettingsRect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUILayout.BeginArea(_guardSettingsRect);
            _guardScrollPos = GUILayout.BeginScrollView(_guardScrollPos, GUILayout.Height(100f));
            GUILayout.Label("驻守设置", _headerStyle);
            GUILayout.Space(6);

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
                _showGuardSettings = false;

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        // ============================================================
        // 巡逻设置浮动窗
        // ============================================================

        void DrawPatrolSettings()
        {
            float w = 260f, h = 280f;
            float x = _panelRect.x + _panelRect.width + 10f;
            float y = _panelRect.y + 160f;
            _patrolSettingsRect = new Rect(x, y, w, h);

            GUI.color = new Color(0.1f, 0.1f, 0.15f, 0.95f);
            GUI.DrawTexture(_patrolSettingsRect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUILayout.BeginArea(_patrolSettingsRect);
            _patrolScrollPos = GUILayout.BeginScrollView(_patrolScrollPos, GUILayout.Height(180f));
            GUILayout.Label("巡逻设置", _headerStyle);
            GUILayout.Space(6);

            // 巡逻模式
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

            // 半径
            GUILayout.Label($"巡逻半径: {_bot.patrolRadius:F0}m", _normalStyle);
            float r = GUILayout.HorizontalSlider(_bot.patrolRadius, _bot.patrolRadiusMin, _bot.patrolRadiusMax,
                GUILayout.Width(200f));
            _bot.patrolRadius = Mathf.Round(r);
            GUILayout.Label($"{_bot.patrolRadiusMin:F0}m ~ {_bot.patrolRadiusMax:F0}m", _smallBtnStyle);

            GUILayout.Space(4);

            // 超距解除
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
                _showPatrolSettings = false;

            GUILayout.EndScrollView();
            GUILayout.EndArea();
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
        }
    }
}
