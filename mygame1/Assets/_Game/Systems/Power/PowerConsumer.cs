using UnityEngine;

namespace _Game.Systems.Power
{
    /// <summary>
    /// 设备用电端组件。挂载在需要电力的设备上。
    /// 通电时电力运转（高速），断电且允许烧煤时切煤模式，否则停摆。
    /// </summary>
    public class PowerConsumer : MonoBehaviour
    {
        [Header("电力需求")]
        public float requiredPower = 30f;

        [Header("双模")]
        public bool allowCoal = false;
        public float coalPower = 15f;

        [Header("电力加成")]
        public float electricSpeedMultiplier = 2f;
        public bool zeroWasteOnElectric = true;

        [Header("显示")]
        public string displayName;

        bool _isPowered;
        bool _isCoalMode;
        bool _manuallyOff;

        public bool IsPowered => _isPowered;
        public bool IsCoalMode => _isCoalMode;
        public bool IsRunning => _isPowered || _isCoalMode;
        public bool IsManuallyOff { get => _manuallyOff; set => SetManualOff(value); }
        public float SpeedMultiplier => _isPowered ? electricSpeedMultiplier : 1f;
        public PowerTerminal ConnectedTerminal { get; private set; }
        public string DisplayName => string.IsNullOrEmpty(displayName) ? gameObject.name : displayName;

        public void SetManualOff(bool off)
        {
            _manuallyOff = off;
            if (off)
            {
                _isPowered = false;
                _isCoalMode = false;
                ConnectedTerminal = null;
            }
            OnStateChanged();
        }

        public float EffectivePowerDraw => _manuallyOff ? 0f : requiredPower;

        public void SetPowered(PowerTerminal terminal, bool powered)
        {
            // 用户手动关掉的设备不自动通电
            if (powered && _manuallyOff) return;

            _isPowered = powered;
            ConnectedTerminal = powered ? terminal : null;

            if (powered)
            {
                _isCoalMode = false;
                OnStateChanged();
            }
            else if (!_manuallyOff && allowCoal)
            {
                _isCoalMode = true;
                OnStateChanged();
            }
            else
            {
                OnStateChanged();
            }
        }

        public void SetCoalMode(bool coal)
        {
            if (!allowCoal) return;
            _isCoalMode = coal;
            if (coal)
            {
                _isPowered = false;
                ConnectedTerminal = null;
            }
            OnStateChanged();
        }

        void OnStateChanged()
        {
            var pd = GetComponent<_Game.Systems.Crafting.ProductionDevice>();
            if (pd != null)
                pd.NotifyPowerState(_isPowered, _isCoalMode);
        }
    }
}
