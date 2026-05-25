using UnityEngine;
using _Game.Config;
using _Game.Core;
using _Game.Systems.Survival;

namespace _Game.Tests
{
    /// <summary>
    /// 生存系统测试脚本
    /// 挂载到 Player 上，调试用
    /// </summary>
    public class SurvivalTest : MonoBehaviour
    {
        [SerializeField] private SurvivalSystem survival;

        private void Awake()
        {
            if (survival == null) survival = GetComponent<SurvivalSystem>();
        }

        void OnEnable()
        {
            InputRouter.BindKey(KeyCode.F5, InputPriority.Debug, () => { survival.ModifyHealth(-10, "测试伤害"); return true; }, this);
            InputRouter.BindKey(KeyCode.F9, InputPriority.Debug, () => { survival.ApplyItemEffect(new ItemEffect { effectType = ItemEffectType.RestoreHunger, value = 30 }); return true; }, this);
            InputRouter.BindKey(KeyCode.F7, InputPriority.Debug, () => { survival.SetSurvivalState(SurvivalStateType.Bleeding, true); return true; }, this);
            InputRouter.BindKey(KeyCode.F8, InputPriority.Debug, () => { survival.SetSurvivalState(SurvivalStateType.Fracture, true); return true; }, this);
        }

        void OnDisable() { InputRouter.UnbindAll(this); }
    }
}
