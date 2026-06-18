using UnityEngine;
using UnityEngine.UI;

namespace _Game.UI
{
    /// <summary>
    /// 红色准星 — 跟随鼠标，装备枪 + 右键瞄准时才显示
    /// </summary>
    public class CrosshairUI : MonoBehaviour
    {
        [Header("外观")]
        public float size = 24f;
        public Color normalColor = new Color(1f, 0.15f, 0.15f, 0.9f);   // 红
        public Color aimColor = new Color(1f, 0f, 0f, 1f);              // 瞄准时更亮

        private GameObject _canvasGo;
        private RectTransform _crossRect;
        private Image _crossImg;
        private _Game.Systems.Weapon.WeaponAiming _aiming;

        void Start()
        {
            _aiming = _Game.Core.PlayerRegistry.Get<_Game.Systems.Weapon.WeaponAiming>();
            CreateUI();
        }

        void CreateUI()
        {
            // Canvas
            _canvasGo = new GameObject("CrosshairCanvas", typeof(Canvas), typeof(CanvasScaler));
            var canvas = _canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999; // 最上层

            // 准星 Image
            var crossObj = new GameObject("Crosshair");
            crossObj.transform.SetParent(_canvasGo.transform, false);
            _crossImg = crossObj.AddComponent<Image>();

            // 画准星 Texture2D — 红色空心圆环 + 中心点
            int texSize = 32;
            var tex = new Texture2D(texSize, texSize, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            Color[] pixels = new Color[texSize * texSize];
            int center = texSize / 2;
            int outerR = texSize / 2 - 2;
            int innerR = outerR - 3;
            for (int y = 0; y < texSize; y++)
            {
                for (int x = 0; x < texSize; x++)
                {
                    int dx = x - center, dy = y - center;
                    int d2 = dx * dx + dy * dy;

                    if (d2 <= outerR * outerR && d2 >= innerR * innerR)
                        pixels[y * texSize + x] = Color.white;             // 圆环
                    else if (d2 <= 2 * 2)
                        pixels[y * texSize + x] = Color.white;             // 中心点
                    else
                        pixels[y * texSize + x] = new Color(0, 0, 0, 0);  // 完全透明
                }
            }
            tex.Apply();
            _crossImg.sprite = Sprite.Create(tex, new Rect(0, 0, texSize, texSize), Vector2.one * 0.5f);
            _crossImg.preserveAspect = true;
            _crossImg.raycastTarget = false;

            _crossRect = crossObj.GetComponent<RectTransform>();
            _crossRect.sizeDelta = new Vector2(size, size);
        }

        void Update()
        {
            bool isAiming = _aiming != null && _aiming.IsAimingDownSights;

            if (isAiming)
            {
                _crossRect.position = Input.mousePosition;
                _crossImg.color = aimColor;
                _crossImg.enabled = true;
            }
            else
            {
                // 不瞄准时也显示淡色准星（方便知道鼠标在哪）
                _crossRect.position = Input.mousePosition;
                _crossImg.color = normalColor * 0.4f; // 半透明
                _crossImg.enabled = true;
            }
        }

        void OnDestroy()
        {
            if (_canvasGo != null) Destroy(_canvasGo);
        }
    }
}
