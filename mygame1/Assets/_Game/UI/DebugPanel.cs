using UnityEngine;
using _Game.Config;
using _Game.Core;
using _Game.Systems.Audio;
using _Game.Systems.Character;
using _Game.Systems.WorldGen;
using _Game.Systems.Zombie;

namespace _Game.UI
{
    /// <summary>
    /// 开发者调试面板，按 Y 切换显示/隐藏。整合所有系统调试信息。
    /// </summary>
    public class DebugPanel : MonoBehaviour
    {
        private bool _visible;
        private Vector2 _scrollPos;

        void OnEnable() { InputRouter.BindKey(KeyCode.Y, InputPriority.Debug, Toggle, this); }
        void OnDisable() { InputRouter.UnbindAll(this); }
        bool Toggle() { _visible = !_visible; return true; }

#if UNITY_EDITOR
        void OnGUI()
        {
            if (!_visible || !Application.isPlaying) return;

            var cm = ChunkManager.Instance;
            var aw = ZombieAwarenessSystem.Instance;
            var db = DecibelSystem.Instance;
            var xp = SurvivalXPSystem.Instance;

            float x = Screen.width - 260f;
            float y = 10f;
            float w = 250f;

            GUILayout.BeginArea(new Rect(x, y, w, Screen.height - 20f));
            _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Width(w), GUILayout.Height(Screen.height - 40f));
            GUILayout.BeginVertical("box");

            GUILayout.Label("<b>Debug Panel  [Y/F2 关闭]</b>", GUILayout.Height(22f));

            // ── Survival XP ──
            if (xp != null)
            {
                GUILayout.Label("━━ Survival XP ━━");
                GUILayout.Label($"  Total XP: {xp.TotalXP}  |  Skill Pts: {xp.AvailablePoints}");

                // --- XP 按钮 ---
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("+50 XP", GUILayout.Height(22f))) xp.AddXP(50);
                if (GUILayout.Button("+100 XP", GUILayout.Height(22f))) xp.AddXP(100);
                if (GUILayout.Button("+500 XP", GUILayout.Height(22f))) xp.AddXP(500);
                GUILayout.EndHorizontal();

                // --- 兑换 & 加点 ---
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("兑换XP→点数", GUILayout.Height(22f)))
                {
                    int got = xp.ConvertXPToPoints();
                    Debug.Log($"[Debug] 兑换 {got} 技能点");
                }
                if (GUILayout.Button("+1 技能点", GUILayout.Height(22f)))
                {
                    // 直接给点数 (通过反射或新增方法) → 用 AddXP + Convert
                    xp.AddXP(GameConstants.XP_PER_SKILL_POINT);
                    xp.ConvertXPToPoints();
                }
                if (GUILayout.Button("+5 技能点", GUILayout.Height(22f)))
                {
                    xp.AddXP(GameConstants.XP_PER_SKILL_POINT * 5);
                    xp.ConvertXPToPoints();
                }
                GUILayout.EndHorizontal();

                GUILayout.Label("  <b>Attributes:</b>");
                foreach (AttributeType attr in System.Enum.GetValues(typeof(AttributeType)))
                    GUILayout.Label($"    {attr}: Lv{xp.GetAttributeValue(attr)}");

                GUILayout.Label("  <b>Skills:</b>");
                foreach (SkillType sk in System.Enum.GetValues(typeof(SkillType)))
                {
                    int lv = xp.GetSkillLevel(sk);
                    int cost = SkillCostTable.GetCost(sk, lv);
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"  {sk}: Lv{lv}  (→{lv + 1}: {cost}pt)", GUILayout.Width(220f));
                    if (lv < 10 && GUILayout.Button("↑", GUILayout.Width(28f), GUILayout.Height(18f)))
                    {
                        if (!xp.SpendPoint(sk))
                            Debug.Log($"[Debug] 升级 {sk} 失败 (点数不足或已满级)");
                    }
                    GUILayout.EndHorizontal();
                }
            }

            // ── Stamina ──
            var stamina = FindObjectOfType<StaminaSystem>();
            if (stamina != null)
            {
                GUILayout.Label("━━ Stamina ━━");
                GUILayout.Label($"  {stamina.CurrentStamina:F0} / {stamina.MaxStamina:F0}  ({stamina.Ratio * 100f:F0}%)");
                if (stamina.IsExhausted)
                    GUILayout.Label("  <color=red>EXHAUSTED</color>");
            }

            // ── Weapons ──
            var switcher = FindObjectOfType<_Game.Systems.Weapon.WeaponSwitcher>();
            if (switcher != null)
            {
                GUILayout.Label("━━ Weapons [Q/滚轮] ━━");
                var slots = new[] { EquipSlot.RightHand, EquipSlot.LeftHand, EquipSlot.KnifeBelt, EquipSlot.SidearmBelt };
                var labels = new[] { "1主武器", "2副武器", "3小刀", "4手枪" };
                var inv = switcher.GetComponent<_Game.Systems.Inventory.Inventory>();
                for (int i = 0; i < slots.Length; i++)
                {
                    bool isActive = switcher.ActiveSlot == slots[i];
                    string marker = isActive ? "▶" : " ";
                    string name = "空";
                    float dmg = 0;
                    if (inv != null && inv.equipped.TryGetValue(slots[i], out var weapon) && weapon != null)
                    {
                        name = weapon.itemName;
                        dmg = weapon.weaponDamage;
                    }
                    if (isActive)
                        GUILayout.Label($"  <color=yellow>{marker} {labels[i]}: {name} (DMG:{dmg})</color>");
                    else
                        GUILayout.Label($"  {marker} {labels[i]}: {name} (DMG:{dmg})");
                }
            }

            // ── Chunks ──
            if (cm != null)
            {
                cm.GetChunkCounts(out int loaded, out int preloaded, out int unloaded);
                GUILayout.Label("━━ Chunks ━━");
                GUILayout.Label($"  L:{loaded}  P:{preloaded}  U:{unloaded}  |  Player: {cm.CurrentPlayerChunk}");
                GUILayout.Label($"  Queue: {cm.PreloadQueueCount}  Speed: {cm.LastPlayerSpeed:F1} m/s  Quality: {cm.quality}");
            }

            // ── Zombies ──
            if (aw != null)
            {
                var sp = ZombieSpawner.Instance;
                int alive = 0;
                if (sp != null && cm != null)
                    alive = sp.GetAliveInChunk(cm.CurrentPlayerChunk);

                GUILayout.Label("━━ Zombies ━━");
                GUILayout.Label($"  Active: {aw.ActiveZombieCount}  Interval: {aw.checkInterval}s");
                if (sp != null)
                    GUILayout.Label($"  CurrentChunk: {alive} alive  Budget: {sp.GetBudgetInChunk(cm?.CurrentPlayerChunk ?? -1)}");
            }

            // ── Decibel ──
            if (db != null)
            {
                GUILayout.Label("━━ Decibel ━━");
                GUILayout.Label($"  Modifiers: {db.ModifierCount}  Listeners: {db.ListenerCount}");
                GUILayout.Label($"  Continuous: {db.ContinuousSoundCount}  Cooldowns: {db.SourceCooldownCount}");
            }

            GUILayout.EndVertical();
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
#endif
    }
}
