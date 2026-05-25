using UnityEngine;
using _Game.Config;
using InventorySystem = _Game.Systems.Inventory.Inventory;
using _Game.UI;

namespace _Game.Systems.Power
{
    /// <summary>
    /// 电缆连线交互系统。从终端面板或电源面板发起"连接"模式，左键点目标完成连线。
    /// 消耗背包中的"电缆"物品。支持 PowerSource→Terminal, Terminal→PowerSource, Terminal→Terminal。
    /// </summary>
    public class CableLinker : MonoBehaviour
    {
        static CableLinker _instance;

        enum LinkMode { None, FromTerminal, FromSource }

        static PowerTerminal _linkTerminal;
        static PowerSource _linkSource;
        static LinkMode _linkMode;
        static bool _linking;

        const float MAX_CABLE_DISTANCE = 30f;
        const string CABLE_ITEM_NAME = "电缆";

        public static void StartLinking(PowerTerminal source)
        {
            EnsureInstance();
            _linkTerminal = source;
            _linkSource = null;
            _linkMode = LinkMode.FromTerminal;
            _linking = true;
        }

        public static void StartLinkingFromSource(PowerSource source)
        {
            EnsureInstance();
            _linkSource = source;
            _linkTerminal = null;
            _linkMode = LinkMode.FromSource;
            _linking = true;
        }

        public static bool IsLinking => _linking;

        static void EnsureInstance()
        {
            if (_instance == null)
            {
                var go = new GameObject("CableLinker");
                _instance = go.AddComponent<CableLinker>();
            }
        }

        void Update()
        {
            if (!_linking) return;

            if (Input.GetMouseButtonDown(1))
            {
                CancelLink();
                return;
            }

            if (Input.GetMouseButtonDown(0))
            {
                TryLink(Input.mousePosition);
            }
        }

        void TryLink(Vector3 screenPos)
        {
            var ray = Camera.main.ScreenPointToRay(screenPos);
            if (!Physics.Raycast(ray, out var hit, 50f)) return;

            Vector3 originPos = _linkMode == LinkMode.FromTerminal
                ? (_linkTerminal != null ? _linkTerminal.transform.position : Vector3.zero)
                : (_linkSource != null ? _linkSource.transform.position : Vector3.zero);

            // 距离检查
            float dist = Vector3.Distance(originPos, hit.point);
            if (dist > MAX_CABLE_DISTANCE)
            {
                Debug.LogWarning($"[CableLinker] 距离太远 ({dist:F1}m > {MAX_CABLE_DISTANCE}m)");
                return;
            }

            // 消耗电缆
            if (!ConsumeCable())
            {
                Debug.LogWarning("[CableLinker] 背包中没有电缆");
                return;
            }

            if (_linkMode == LinkMode.FromTerminal && _linkTerminal != null)
            {
                LinkFromTerminal(hit);
            }
            else if (_linkMode == LinkMode.FromSource && _linkSource != null)
            {
                LinkFromSource(hit);
            }
        }

        void LinkFromTerminal(RaycastHit hit)
        {
            // 尝试连接另一个终端
            var terminal = hit.collider.GetComponentInParent<PowerTerminal>();
            if (terminal != null && terminal != _linkTerminal)
            {
                _linkTerminal.ConnectTerminal(terminal);
                PowerLineRenderer.Create(_linkTerminal.transform, terminal.transform);
                Debug.Log("[CableLinker] 终端 ↔ 终端 已连接");
                FinishLink();
                return;
            }

            // 尝试连接发电端
            var source = hit.collider.GetComponentInParent<PowerSource>();
            if (source != null)
            {
                _linkTerminal.ConnectSource(source);
                PowerLineRenderer.Create(source.transform, _linkTerminal.transform);
                Debug.Log($"[CableLinker] 发电端 {source.sourceType} → 终端 已连接");
                FinishLink();
                return;
            }
        }

        void LinkFromSource(RaycastHit hit)
        {
            // 电源只能连接终端
            var terminal = hit.collider.GetComponentInParent<PowerTerminal>();
            if (terminal != null)
            {
                terminal.ConnectSource(_linkSource);
                PowerLineRenderer.Create(_linkSource.transform, terminal.transform);
                Debug.Log($"[CableLinker] 发电端 {_linkSource.sourceType} → 终端 已连接");
                FinishLink();
                return;
            }
        }

        void FinishLink()
        {
            _linking = false;

            if (_linkMode == LinkMode.FromTerminal && _linkTerminal != null)
                TerminalUI.Show(_linkTerminal);
            else if (_linkMode == LinkMode.FromSource && _linkSource != null)
                PowerSourceUI.Show(_linkSource);
        }

        void CancelLink()
        {
            _linking = false;
            if (_linkMode == LinkMode.FromTerminal && _linkTerminal != null)
                TerminalUI.Show(_linkTerminal);
            else if (_linkMode == LinkMode.FromSource && _linkSource != null)
                PowerSourceUI.Show(_linkSource);
            Debug.Log("[CableLinker] 连线已取消");
        }

        bool ConsumeCable()
        {
            var item = FindCableItem();
            if (item == null) return false;

            var inv = FindObjectOfType<InventorySystem>();
            if (inv == null) return false;

            if (inv.HasItem(item, 1))
            {
                inv.RemoveItem(item, 1);
                return true;
            }
            return false;
        }

        static ItemData FindCableItem()
        {
#if UNITY_EDITOR
            var guids = UnityEditor.AssetDatabase.FindAssets("t:ItemData");
            foreach (var g in guids)
            {
                var data = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemData>(
                    UnityEditor.AssetDatabase.GUIDToAssetPath(g));
                if (data != null && data.itemName == CABLE_ITEM_NAME)
                    return data;
            }
#endif
            return null;
        }

        public static int CountCables()
        {
            var item = FindCableItem();
            if (item == null) return 0;
            var inv = Object.FindObjectOfType<InventorySystem>();
            if (inv == null) return 0;
            return inv.GetItemCount(item);
        }

        void OnGUI()
        {
            if (!_linking) return;

            string modeLabel = _linkMode == LinkMode.FromSource ? "电源 → 终端" : "终端 → 目标";
            string text = $"⚡ {modeLabel}  左键点击目标  右键取消  (最大{MAX_CABLE_DISTANCE}m)";

            float w = 420f, h = 40f;
            GUI.color = new Color(0f, 0f, 0f, 0.85f);
            GUI.DrawTexture(new Rect((Screen.width - w) * 0.5f, Screen.height * 0.1f, w, h), Texture2D.whiteTexture);
            GUI.color = Color.white;
            var s = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.yellow }
            };
            GUI.Label(new Rect((Screen.width - w) * 0.5f, Screen.height * 0.1f, w, h), text, s);
        }
    }
}
