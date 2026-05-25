using System.Collections.Generic;
using UnityEngine;
using _Game.Core;
using _Game.Systems.Interaction;
using _Game.UI;

namespace _Game.Systems.Power
{
    /// <summary>
    /// 用电终端 — 电网中枢。放置后供电范围内设备自动通电。
    /// IInteractable（E键打开终端面板）。
    /// 终端之间可用电缆串联，全电网共享总功率池。
    /// </summary>
    public class PowerTerminal : MonoBehaviour, IInteractable
    {
        [Header("供电")]
        public float supplyRadius = 15f;

        [Header("备用燃料")]
        [Tooltip("无发电端时可烧煤兜底")]
        public bool allowCoalBackup = true;

        [Header("状态")]
        public List<PowerSource> connectedSources = new List<PowerSource>();
        public List<PowerTerminal> connectedTerminals = new List<PowerTerminal>();

        float _scanTimer;
        const float SCAN_INTERVAL = 1f;

        // 缓存
        float _cachedGridPower;
        float _cachedGridLoad;
        bool _gridDirty = true;
        List<PowerConsumer> _cachedConsumers = new List<PowerConsumer>();

        public List<PowerConsumer> ConsumersInRange => _cachedConsumers;

        // IInteractable
        string IInteractable.InteractionPrompt => "用电终端 [E]";
        float IInteractable.InteractionTime => 0f;
        bool IInteractable.IsInteractable => enabled;

        void IInteractable.OnInteract(GameObject interactor)
        {
            TerminalUI.Show(this);
        }

        void OnEnable() => _gridDirty = true;
        void OnDisable()
        {
            // 断开时从所有邻居的列表中移除自己
            foreach (var t in connectedTerminals)
                if (t != null) t.connectedTerminals.Remove(this);
            connectedTerminals.Clear();
        }

        void Update()
        {
            _scanTimer += UnityEngine.Time.deltaTime;
            if (_scanTimer >= SCAN_INTERVAL)
            {
                _scanTimer = 0f;
                ScanAndPower();
            }
        }

        // ============================================================
        // 电网计算
        // ============================================================

        void ScanAndPower()
        {
            // 遍历全电网
            var visited = new HashSet<PowerTerminal>();
            float totalPower = 0f;
            float totalLoad = 0f;
            CollectGrid(this, visited, ref totalPower);

            // 扫描每个终端的范围内的设备负载
            var allConsumers = new List<PowerConsumer>();
            foreach (var t in visited)
                allConsumers.AddRange(FindConsumersInRadius(t));

            foreach (var c in allConsumers)
                totalLoad += c.EffectivePowerDraw;

            _cachedGridPower = totalPower;
            _cachedGridLoad = totalLoad;
            _cachedConsumers = allConsumers;

            bool gridOk = totalPower >= totalLoad;

            // 给范围内设备通电/断电
            foreach (var c in allConsumers)
                c.SetPowered(this, gridOk && c.EffectivePowerDraw <= totalPower - (totalLoad - c.EffectivePowerDraw));

            // 更新所有终端缓存
            foreach (var t in visited)
            {
                t._cachedGridPower = totalPower;
                t._cachedGridLoad = totalLoad;
                t._gridDirty = false;
            }
        }

        void CollectGrid(PowerTerminal start, HashSet<PowerTerminal> visited, ref float totalPower)
        {
            if (!visited.Add(start)) return;

            foreach (var src in start.connectedSources)
                if (src != null && src.IsActive)
                    totalPower += src.maxOutput;

            foreach (var t in start.connectedTerminals)
                if (t != null)
                    CollectGrid(t, visited, ref totalPower);
        }

        List<PowerConsumer> FindConsumersInRadius(PowerTerminal terminal)
        {
            var result = new List<PowerConsumer>();
            var all = FindObjectsOfType<PowerConsumer>();
            foreach (var c in all)
            {
                if (!c.enabled || !c.gameObject.activeInHierarchy) continue;
                float dist = Vector3.Distance(terminal.transform.position, c.transform.position);
                if (dist <= terminal.supplyRadius)
                    result.Add(c);
            }
            return result;
        }

        // ============================================================
        // 公共 API
        // ============================================================

        public float GridPower => _cachedGridPower;
        public float GridLoad => _cachedGridLoad;
        public bool IsGridOk => _cachedGridPower >= _cachedGridLoad && _cachedGridPower > 0;

        public void ConnectSource(PowerSource source)
        {
            if (source == null || connectedSources.Contains(source)) return;
            connectedSources.Add(source);
            _gridDirty = true;
        }

        public void DisconnectSource(PowerSource source)
        {
            connectedSources.Remove(source);
            PowerLineRenderer.RemoveBetween(source.transform, transform);
            _gridDirty = true;
        }

        public void ConnectTerminal(PowerTerminal other)
        {
            if (other == null || other == this || connectedTerminals.Contains(other)) return;
            connectedTerminals.Add(other);
            if (!other.connectedTerminals.Contains(this))
                other.connectedTerminals.Add(this);
            _gridDirty = true;
            other._gridDirty = true;
        }

        public void DisconnectTerminal(PowerTerminal other)
        {
            connectedTerminals.Remove(other);
            PowerLineRenderer.RemoveBetween(transform, other != null ? other.transform : null);
            other?.connectedTerminals.Remove(this);
            _gridDirty = true;
            if (other != null) other._gridDirty = true;
        }

        /// <summary>建造预览时绘制供电圈</summary>
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.9f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, supplyRadius);
        }
    }
}
