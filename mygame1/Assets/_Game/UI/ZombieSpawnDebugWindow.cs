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


        void SpawnZombies()
        {
            if (_availableTypes.Count == 0)
            {
                ShowMessage("错误: 没有可用的僵尸类型");
                return;
            }

            if (!PlayerRegistry.Exists)
            {
                ShowMessage("错误: 找不到玩家");
                return;
            }

            ZombieData data = _availableTypes[_selectedTypeIndex];
            Vector3 playerPos = PlayerRegistry.Position;
            Vector3 playerForward = PlayerRegistry.Transform.forward;
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
