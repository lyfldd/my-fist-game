using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using _Game.Config;
using _Game.Core;
using _Game.Systems.Combat;
using _Game.Systems.WorldGen;
using _Game.Systems.Zombie;

namespace _Game.UI
{
    /// <summary>
    /// F1 开发者调试窗口：配置并刷僵尸。距离/类型/数量可调。
    /// </summary>
    public class ZombieSpawnDebugWindow : MonoBehaviour
    {
        private bool _visible;
        private float _distance = 15f;
        private int _count = 3;
        private int _selectedTypeIndex;
        private readonly List<ZombieData> _availableTypes = new List<ZombieData>();
        private string _lastMessage;
        private float _messageTimer;

        void Start() => RefreshTypes();

        void OnEnable() { InputRouter.BindKey(KeyCode.F6, InputPriority.Debug, Toggle, this); }
        void OnDisable() { InputRouter.UnbindAll(this); }

        bool Toggle() { _visible = !_visible; return true; }

        void Update()
        {
            if (_messageTimer > 0f)
                _messageTimer -= UnityEngine.Time.deltaTime;
        }

        void RefreshTypes()
        {
            _availableTypes.Clear();
            var spawner = ZombieSpawner.Instance;
            if (spawner != null && spawner.zoneProfiles != null)
            {
                var seen = new HashSet<ZombieData>();
                foreach (var profile in spawner.zoneProfiles)
                {
                    if (profile?.typeWeights == null) continue;
                    foreach (var tw in profile.typeWeights)
                    {
                        if (tw.data != null && seen.Add(tw.data))
                            _availableTypes.Add(tw.data);
                    }
                }
            }
            if (_availableTypes.Count > 0 && _selectedTypeIndex >= _availableTypes.Count)
                _selectedTypeIndex = 0;
        }

#if UNITY_EDITOR
        void OnGUI()
        {
            if (!_visible || !Application.isPlaying) return;

            float w = 320f;
            float h = 260f;
            float x = (Screen.width - w) * 0.5f;
            float y = (Screen.height - h) * 0.5f;

            GUI.Box(new Rect(x, y, w, h), "僵尸刷新调试 [F6 关闭]");
            GUILayout.BeginArea(new Rect(x + 10, y + 25, w - 20, h - 35));
            GUILayout.BeginVertical();

            // ── 距离 ──
            GUILayout.BeginHorizontal();
            GUILayout.Label("玩家距离 (m):", GUILayout.Width(100));
            _distance = GUILayout.HorizontalSlider(_distance, 2f, 50f, GUILayout.Width(120));
            string distText = GUILayout.TextField(_distance.ToString("F0"), GUILayout.Width(40));
            if (float.TryParse(distText, out float d) && d >= 1f && d <= 100f)
                _distance = d;
            GUILayout.EndHorizontal();

            GUILayout.Space(4);

            // ── 类型 ──
            GUILayout.Label("僵尸类型:");
            if (_availableTypes.Count > 0)
            {
                string[] names = new string[_availableTypes.Count];
                for (int i = 0; i < _availableTypes.Count; i++)
                    names[i] = _availableTypes[i].zombieName;
                int cols = Mathf.Min(names.Length, 3);
                _selectedTypeIndex = GUILayout.SelectionGrid(_selectedTypeIndex, names, cols);
            }
            else
            {
                GUILayout.Label("  (无可用类型 — 请先配置 ZombieSpawner.zoneProfiles)");
            }

            GUILayout.Space(4);

            // ── 数量 ──
            GUILayout.BeginHorizontal();
            GUILayout.Label("数量:", GUILayout.Width(100));
            _count = Mathf.RoundToInt(GUILayout.HorizontalSlider(_count, 1, 30));
            string countText = GUILayout.TextField(_count.ToString(), GUILayout.Width(40));
            if (int.TryParse(countText, out int c) && c >= 1 && c <= 100)
                _count = c;
            GUILayout.EndHorizontal();

            GUILayout.Space(8);

            // ── 按钮 ──
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("刷新类型列表", GUILayout.Height(28)))
                RefreshTypes();

            GUI.backgroundColor = new Color(0.85f, 0.25f, 0.25f);
            if (GUILayout.Button($"刷新 {_count} 只僵尸", GUILayout.Height(28)))
                SpawnZombies();
            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();

            // ── 消息 ──
            if (_messageTimer > 0f)
            {
                GUILayout.Space(4);
                GUILayout.Label(_lastMessage);
            }

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
#endif

        void SpawnZombies()
        {
            if (_availableTypes.Count == 0)
            {
                ShowMessage("错误: 没有可用的僵尸类型");
                return;
            }

            var playerObj = GameObject.FindWithTag("Player") ?? GameObject.Find("Player");
            if (playerObj == null)
            {
                ShowMessage("错误: 找不到玩家");
                return;
            }

            ZombieData data = _availableTypes[_selectedTypeIndex];
            Vector3 playerPos = playerObj.transform.position;
            Vector3 playerForward = playerObj.transform.forward;
            int spawned = 0;

            for (int i = 0; i < _count; i++)
            {
                float angle = Random.Range(-100f, 100f);
                Vector3 dir = Quaternion.Euler(0, angle, 0) * (-playerForward);
                Vector3 spawnPos = playerPos + dir.normalized * _distance;

                if (NavMesh.SamplePosition(spawnPos, out NavMeshHit hit, 5f, NavMesh.AllAreas))
                    spawnPos = hit.position;
                else
                    continue;

                var go = new GameObject($"DebugZombie_{data.zombieName}_{spawned}");
                go.transform.position = spawnPos;

                var agent = go.AddComponent<NavMeshAgent>();
                var stateMachine = go.AddComponent<ZombieStateMachine>();
                var damageable = go.AddComponent<DamageableZombie>();
                var controller = go.AddComponent<ZombieController>();
                controller.Initialize(data);

                // 圆柱体外形（复用 Spawner 的静态方法）
                ZombieSpawner.BuildBody(go, data);

                int chunkId = ChunkManager.GetChunkId(spawnPos);
                ChunkManager.Instance?.RegisterZombie(stateMachine, chunkId);

                // AIAgent 自行处理感知，不再需要手动注册
                agent.enabled = true;

                spawned++;
            }

            ShowMessage($"已刷新 {spawned}/{_count} 只 {data.zombieName} (距离 {_distance:F0}m)");
        }

        void ShowMessage(string msg)
        {
            _lastMessage = msg;
            _messageTimer = 3f;
        }
    }
}
