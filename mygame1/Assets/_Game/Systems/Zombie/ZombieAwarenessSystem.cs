using System.Collections.Generic;
using UnityEngine;

namespace _Game.Systems.Zombie
{
    /// <summary>
    /// [已废弃] 集中式玩家检测系统。
    /// 功能已完全迁移到 AIAgent 基类的 PerceptionTick + ThreatSystem。
    /// 保留此类为空壳以避免编译错误，所有方法均为 no-op。
    /// </summary>
    [System.Obsolete("功能已迁移到 AIAgent 基类。僵尸通过 AIAgent.PerceptionTick + ThreatSystem 自行感知。")]
    public class ZombieAwarenessSystem : MonoBehaviour
    {
        public static ZombieAwarenessSystem Instance { get; private set; }

        [Header("已废弃")]
        public float checkInterval = 0.5f;
        public float cascadeRadius = 15f;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void Register(ZombieStateMachine zombie) { /* no-op: AIAgent 自行管理 */ }
        public void Unregister(ZombieStateMachine zombie) { /* no-op */ }
        public void RegisterAIBot(Transform aiBot) { /* no-op: AIAgent 通过 ThreatSystem 感知 */ }
        public void UnregisterAIBot(Transform aiBot) { /* no-op */ }
        public int ActiveZombieCount => 0;
    }
}
