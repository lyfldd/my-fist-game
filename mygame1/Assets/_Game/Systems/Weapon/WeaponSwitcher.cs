using UnityEngine;
using _Game.Config;
using _Game.Core;
using _Game.Systems.Combat;
using Inv = _Game.Systems.Inventory.Inventory;

namespace _Game.Systems.Weapon
{
    public class WeaponSwitcher : MonoBehaviour
    {
        private EquipSlot _activeSlot = EquipSlot.None;
        private Inv _inventory;
        private PlayerCombat _playerCombat;
        private WeaponShooting _weaponShooting;

        public EquipSlot ActiveSlot => _activeSlot;

        public ItemData ActiveWeapon
        {
            get
            {
                if (_activeSlot == EquipSlot.None) return null;
                if (_inventory != null && _inventory.equipped.TryGetValue(_activeSlot, out var w) && w != null)
                    return w;
                return null;
            }
        }

        void Awake()
        {
            _inventory = GetComponent<Inv>();
            _playerCombat = GetComponent<PlayerCombat>();
            _weaponShooting = GetComponent<WeaponShooting>();
        }

        private static readonly EquipSlot[] _cycleOrder =
            { EquipSlot.RightHand, EquipSlot.LeftHand, EquipSlot.KnifeBelt, EquipSlot.SidearmBelt };

        void Start()
        {
            // 找第一个有武器的槽，没有则默认 RightHand
            foreach (var slot in _cycleOrder)
            {
                if (HasWeapon(slot))
                {
                    SwitchTo(slot);
                    return;
                }
            }
            SwitchTo(EquipSlot.None);
        }

        void OnEnable() { InputRouter.BindKey(KeyCode.Q, InputPriority.Gameplay, HandleCycleWeapon, this); }
        void OnDisable() { InputRouter.UnbindAll(this); }

        bool HandleCycleWeapon() { CycleNext(); return true; }

        void CycleNext()
        {
            int start = System.Array.IndexOf(_cycleOrder, _activeSlot);
            if (start < 0) start = -1;
            for (int i = 1; i <= _cycleOrder.Length; i++)
            {
                int idx = (start + i) % _cycleOrder.Length;
                if (HasWeapon(_cycleOrder[idx]))
                {
                    SwitchTo(_cycleOrder[idx]);
                    return;
                }
            }
            // 没有任何武器 → 空手
            SwitchTo(EquipSlot.None);
        }

        void CyclePrevious()
        {
            int start = System.Array.IndexOf(_cycleOrder, _activeSlot);
            if (start < 0) start = 0;
            for (int i = 1; i <= _cycleOrder.Length; i++)
            {
                int idx = (start - i + _cycleOrder.Length) % _cycleOrder.Length;
                if (HasWeapon(_cycleOrder[idx]))
                {
                    SwitchTo(_cycleOrder[idx]);
                    return;
                }
            }
            SwitchTo(EquipSlot.None);
        }

        bool HasWeapon(EquipSlot slot)
        {
            return _inventory != null
                && _inventory.equipped.TryGetValue(slot, out var w)
                && w != null;
        }

        void SwitchTo(EquipSlot slot)
        {
            if (_activeSlot == slot) return;
            _activeSlot = slot;

            var weapon = ActiveWeapon;
            bool isFirearm = weapon != null && weapon.isFirearm;

            if (_weaponShooting != null)
                _weaponShooting.enabled = isFirearm;
            if (_playerCombat != null)
                _playerCombat.enabled = !isFirearm;

            EventBus.Publish(new WeaponSlotChangedEvent(slot, weapon));
        }

        public void ForceSwitch(EquipSlot slot)
        {
            _activeSlot = EquipSlot.None;
            SwitchTo(slot);
        }
    }
}
