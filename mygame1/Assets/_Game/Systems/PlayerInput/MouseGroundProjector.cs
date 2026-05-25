using UnityEngine;
using _Game.Core;

namespace _Game.Systems.PlayerInput
{
    /// <summary>
    /// 鼠标→地面投影 — 挂在玩家上，每帧 Update 算一次，各系统读取 GroundPoint。
    /// WeaponAiming / GhostPreview / PlayerController 均通过 GetComponent 引用。
    /// </summary>
    public class MouseGroundProjector : MonoBehaviour
    {
        [Header("地面平面")]
        public float groundY = GameConstants.GROUND_PLANE_Y;

        /// <summary> 鼠标在地面平面的世界坐标（每帧 Update 更新）</summary>
        public Vector3 GroundPoint { get; private set; }

        /// <summary> 是否有有效投影点（射线未命中时为 false）</summary>
        public bool HasValidTarget { get; private set; }

        private Camera _cam;

        void Awake()
        {
            _cam = Camera.main;
        }

        void Update()
        {
            if (_cam == null)
            {
                HasValidTarget = false;
                return;
            }

            Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
            Plane ground = new Plane(Vector3.up, new Vector3(0, groundY, 0));

            if (ground.Raycast(ray, out float enter))
            {
                GroundPoint = ray.GetPoint(enter);
                HasValidTarget = true;
            }
            else
            {
                HasValidTarget = false;
            }
        }
    }
}
