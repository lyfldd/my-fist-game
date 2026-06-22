using UnityEngine;
using UnityEngine.UI;
using _Game.Core;

namespace _Game.UI
{
    /// <summary>
    /// 红色准星 — 跟随鼠标，装备枪 + 右键瞄准时才显示
    /// </summary>
    public class CrosshairUI : MonoBehaviour
    {
        [Header("外观")]
        public float size = 24f;
        public Color normalColor = new Color(1f, 0.15f, 0.15f, 0.9f);
        public Color aimColor = new Color(1f, 0f, 0f, 1f);

        private GameObject _canvasGo;
        private RectTransform _crossRect;
        private Image _crossImg;
        private _Game.Systems.Weapon.WeaponAiming _aiming;

        void Start()
        {
            if (!UIModeConfig.UseUGUI) { enabled = false; return; }
            _aiming = PlayerRegistry.Get<_Game.Systems.Weapon.WeaponAiming>();
            CreateUI();
        }

        void CreateUI()
        {
            // 优先使用 PreconfigureUI 预构建的
            var existing = transform.Find("CrosshairCanvas");
            if (existing != null) { _canvasGo = existing.gameObject; _crossImg = _canvasGo.transform.Find("Crosshair")?.GetComponent<Image>(); _crossRect = _crossImg?.GetComponent<RectTransform>(); return; }

            var canvas = UGUIBuilder.CreateCanvas("CrosshairCanvas", 999);
            _canvasGo = canvas.gameObject;

            var crossGo = new GameObject("Crosshair", typeof(RectTransform));
            crossGo.transform.SetParent(_canvasGo.transform, false);
            _crossImg = crossGo.AddComponent<Image>();

            // 程序化准星纹理 (空心圆环 + 中心点)
            int texSize = 32;
            var tex = new Texture2D(texSize, texSize, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            Color[] pixels = new Color[texSize * texSize];
            int center = texSize / 2, outerR = center - 2, innerR = outerR - 3;
            for (int y = 0; y < texSize; y++)
                for (int x = 0; x < texSize; x++)
                {
                    int dx = x - center, dy = y - center, d2 = dx * dx + dy * dy;
                    bool ring = d2 <= outerR * outerR && d2 >= innerR * innerR;
                    bool dot = d2 <= 4;
                    pixels[y * texSize + x] = (ring || dot) ? Color.white : new Color(0, 0, 0, 0);
                }
            tex.Apply();
            _crossImg.sprite = Sprite.Create(tex, new Rect(0, 0, texSize, texSize), Vector2.one * 0.5f);
            _crossImg.preserveAspect = true;
            _crossImg.raycastTarget = false;

            _crossRect = crossGo.GetComponent<RectTransform>();
            _crossRect.sizeDelta = new Vector2(size, size);
        }

        void Update()
        {
            bool isAiming = _aiming != null && _aiming.IsAimingDownSights;
            _crossRect.position = Input.mousePosition;
            _crossImg.color = isAiming ? aimColor : normalColor * 0.4f;
            _crossImg.enabled = true;
        }

        void OnDestroy()
        {
            if (_canvasGo != null) Destroy(_canvasGo);
        }
    }
}
