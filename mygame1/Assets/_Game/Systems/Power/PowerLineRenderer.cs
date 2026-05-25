using UnityEngine;

namespace _Game.Systems.Power
{
    /// <summary>
    /// 电缆连线可视化。在两个设备之间绘制一条电线。
    /// 挂在新建的中间 GameObject 上，两端跟随 transform。
    /// </summary>
    public class PowerLineRenderer : MonoBehaviour
    {
        Transform _a;
        Transform _b;
        LineRenderer _lr;

        public static PowerLineRenderer Create(Transform a, Transform b)
        {
            var go = new GameObject($"PowerLine_{a.name}_{b.name}");
            var plr = go.AddComponent<PowerLineRenderer>();
            plr.Init(a, b);
            return plr;
        }

        /// <summary>
        /// 断开连接时清理连线。a 和 b 为两个端点的 Transform（顺序无关）。
        /// </summary>
        public static void RemoveBetween(Transform a, Transform b)
        {
            if (a == null || b == null) return;
            var all = FindObjectsOfType<PowerLineRenderer>();
            foreach (var plr in all)
            {
                if (plr == null) continue;
                if ((plr._a == a && plr._b == b) || (plr._a == b && plr._b == a))
                {
                    Destroy(plr.gameObject);
                    return;
                }
            }
        }

        void Init(Transform a, Transform b)
        {
            _a = a;
            _b = b;

            _lr = gameObject.AddComponent<LineRenderer>();
            _lr.positionCount = 2;
            _lr.startWidth = 0.06f;
            _lr.endWidth = 0.06f;
            _lr.material = new Material(Shader.Find("Sprites/Default"));
            _lr.startColor = new Color(0.85f, 0.65f, 0.15f, 0.9f);
            _lr.endColor = new Color(0.85f, 0.65f, 0.15f, 0.9f);
            _lr.sortingOrder = 1;
        }

        void Update()
        {
            if (_a == null || _b == null)
            {
                Destroy(gameObject);
                return;
            }
            _lr.SetPosition(0, _a.position);
            _lr.SetPosition(1, _b.position);
        }

        void OnDestroy()
        {
            if (_lr != null)
                Destroy(_lr.material);
        }
    }
}
