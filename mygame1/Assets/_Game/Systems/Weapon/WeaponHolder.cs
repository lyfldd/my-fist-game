using System.Collections.Generic;
using UnityEngine;
using _Game.Config;
using _Game.Core;

namespace _Game.Systems.Weapon
{
    public class WeaponHolder : MonoBehaviour
    {
        [Header("骨骼配置")]
        public string handBoneName = "RightHand";

        [Header("Capsule 模式偏移")]
        public Vector3 capsuleHandOffset = GameConstants.CAPSULE_HAND_OFFSET;
        public Vector3 capsuleHandEuler = GameConstants.CAPSULE_HAND_EULER;

        private Transform _rightHandPoint;
        private bool _usingBone;
        private Dictionary<EquipSlot, GameObject> _models = new Dictionary<EquipSlot, GameObject>();
        private WeaponSwitcher _switcher;

        public Vector3 AimDirection { get; set; } = Vector3.forward;
        public Vector3 HandWorldPos => _rightHandPoint != null ? _rightHandPoint.position : transform.position + Vector3.up * GameConstants.PLAYER_HAND_HEIGHT;

        void Awake()
        {
            CreateHandPoint();
            _switcher = GetComponent<WeaponSwitcher>();
            EventBus.Subscribe<WeaponEquippedEvent>(OnWeaponEquipped);
            EventBus.Subscribe<WeaponUnequippedEvent>(OnWeaponUnequipped);
            EventBus.Subscribe<WeaponSlotChangedEvent>(OnSlotChanged);
        }

        void CreateHandPoint()
        {
            Transform bone = FindHandBone(handBoneName);
            if (bone != null)
            {
                _rightHandPoint = bone;
                _usingBone = true;
            }
            else
            {
                var go = new GameObject("RightHandPoint");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = capsuleHandOffset;
                go.transform.localEulerAngles = capsuleHandEuler;
                _rightHandPoint = go.transform;
                _usingBone = false;
            }
        }

        Transform FindHandBone(string boneName)
        {
            if (string.IsNullOrEmpty(boneName)) return null;
            foreach (var t in GetComponentsInChildren<Transform>())
                if (t.name.Contains(boneName) || t.name.ToLower().Contains(boneName.ToLower()))
                    return t;
            return null;
        }

        void LateUpdate()
        {
            if (!_usingBone && _rightHandPoint != null)
            {
                _rightHandPoint.localPosition = capsuleHandOffset;
                _rightHandPoint.localEulerAngles = capsuleHandEuler;
            }

            EquipSlot activeSlot = _switcher != null ? _switcher.ActiveSlot : EquipSlot.None;
            if (_models.TryGetValue(activeSlot, out var activeModel) && activeModel != null && AimDirection.sqrMagnitude > 0.001f)
            {
                var flatDir = new Vector3(AimDirection.x, 0, AimDirection.z);
                if (flatDir.sqrMagnitude > 0.001f)
                {
                    var targetRot = Quaternion.LookRotation(flatDir, Vector3.up);
                    activeModel.transform.rotation = Quaternion.Slerp(
                        activeModel.transform.rotation,
                        targetRot,
                        GameConstants.WEAPON_ROTATION_SMOOTH_SPEED * UnityEngine.Time.deltaTime
                    );
                }
            }
        }

        void OnWeaponEquipped(WeaponEquippedEvent evt)
        {
            if (evt.Item == null) return;
            SpawnModel(evt.Item, evt.Slot);
        }

        void OnWeaponUnequipped(WeaponUnequippedEvent evt)
        {
            if (_models.TryGetValue(evt.Slot, out var model) && model != null)
            {
                Destroy(model);
                _models.Remove(evt.Slot);
            }
        }

        void OnSlotChanged(WeaponSlotChangedEvent evt)
        {
            foreach (var kv in _models)
                if (kv.Value != null) kv.Value.SetActive(kv.Key == evt.Slot);
        }

        void SpawnModel(ItemData item, EquipSlot slot)
        {
            if (_models.TryGetValue(slot, out var old) && old != null)
                Destroy(old);

            if (item.worldPrefab == null) return;

            var model = Instantiate(item.worldPrefab, _rightHandPoint);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;

            bool isActive = _switcher != null && _switcher.ActiveSlot == slot;
            model.SetActive(isActive);
            _models[slot] = model;
        }

        void OnDestroy()
        {
            EventBus.Unsubscribe<WeaponEquippedEvent>(OnWeaponEquipped);
            EventBus.Unsubscribe<WeaponUnequippedEvent>(OnWeaponUnequipped);
            EventBus.Unsubscribe<WeaponSlotChangedEvent>(OnSlotChanged);
        }
    }
}
