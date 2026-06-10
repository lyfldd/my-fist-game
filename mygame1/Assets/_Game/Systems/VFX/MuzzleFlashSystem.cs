using UnityEngine;
using _Game.Core;

namespace _Game.Systems.VFX
{
    /// <summary>
    /// 枪口火焰系统 — 订阅 WeaponFiredEvent，按 vfxType 生成对应大小的枪口火焰。
    /// Phase 4 需要提供 Small/Medium/Large 三种火焰 prefab。
    /// </summary>
    public class MuzzleFlashSystem : MonoBehaviour
    {
        [Header("枪口火焰 Prefab（Phase 4 提供）")]
        public GameObject smallFlashPrefab;
        public GameObject mediumFlashPrefab;
        public GameObject largeFlashPrefab;

        [Header("配置")]
        public float flashLifetime = 0.08f;

        void Awake()
        {
            if (smallFlashPrefab == null) smallFlashPrefab = Resources.Load<GameObject>("VFX/MuzzleFlash_Small");
            if (mediumFlashPrefab == null) mediumFlashPrefab = Resources.Load<GameObject>("VFX/MuzzleFlash_Medium");
            if (largeFlashPrefab == null) largeFlashPrefab = Resources.Load<GameObject>("VFX/MuzzleFlash_Large");
        }

        void OnEnable()
        {
            EventBus.Subscribe<WeaponFiredEvent>(OnWeaponFired);
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<WeaponFiredEvent>(OnWeaponFired);
        }

        void OnWeaponFired(WeaponFiredEvent evt)
        {
            if (string.IsNullOrEmpty(evt.VfxType)) return;

            var prefab = evt.VfxType switch
            {
                "Small" => smallFlashPrefab,
                "Medium" => mediumFlashPrefab,
                "Large" => largeFlashPrefab,
                _ => null
            };

            if (prefab == null) return;

            var flash = Instantiate(prefab, evt.MuzzlePos,
                Quaternion.LookRotation(evt.Direction));
            Destroy(flash, flashLifetime);
        }
    }
}
