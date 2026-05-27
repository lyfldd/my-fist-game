using UnityEngine;
using UnityEngine.AI;
using _Game.Core;

namespace _Game.Systems.AIBot
{
    /// <summary>
    /// AI机器人驾驶控制器。玩家可驾驶机器人，手动控制一门武器，其余AI自动。
    /// 挂载在机器人GameObject上。
    /// </summary>
    [RequireComponent(typeof(AIBot))]
    [RequireComponent(typeof(NavMeshAgent))]
    public class AIBotPilot : MonoBehaviour
    {
        [Header("驾驶设置")]
        [SerializeField] private float maxPilotDistance = 100f;
        [SerializeField] private float moveSpeed = 8f;
        [SerializeField] private float rotationSpeed = 360f;

        public bool IsPiloting { get; private set; }
        public GameObject Pilot { get; private set; }
        public AttackPriority ManualWeaponSlot { get; private set; } = AttackPriority.Laser;

        private AIBot _bot;
        private AIBotCombat _combat;
        private NavMeshAgent _agent;
        private AIBotCommand _previousCommand;
        private Vector3 _aimPosition;

        void Awake()
        {
            _bot = GetComponent<AIBot>();
            _combat = GetComponent<AIBotCombat>();
            if (_combat == null)
                _combat = gameObject.AddComponent<AIBotCombat>();
            _agent = GetComponent<NavMeshAgent>();
        }

        void Update()
        {
            if (!IsPiloting) return;

            // 退出检测
            if (_bot.IsDead || _bot.IsShutdown || Pilot == null)
            {
                ExitPilot();
                return;
            }

            float dist = Vector3.Distance(transform.position, Pilot.transform.position);
            if (dist > maxPilotDistance)
            {
                Debug.Log("[AIBotPilot] 距离过远，自动退出驾驶");
                ExitPilot();
                return;
            }

            // WASD 移动
            HandleMovement();

            // Q 切换武器（直接用Input，绕过InputRouter）
            if (Input.GetKeyDown(KeyCode.Q))
            {
                CycleManualWeapon();
                Debug.Log($"[AIBotPilot] Q切换武器 → {ManualWeaponSlot}");
            }

            // 鼠标瞄准（非激光模式下需要）
            if (ManualWeaponSlot != AttackPriority.Laser)
                HandleAiming();

            // 左键开火（直接用Input）
            if (Input.GetMouseButtonDown(0))
                ManualFire();

            // E 打开AI面板（直接用Input）
            if (Input.GetKeyDown(KeyCode.E))
                ToggleAIPanel();
        }

        void HandleMovement()
        {
            Vector3 moveDir = Vector3.zero;
            if (Input.GetKey(KeyCode.W)) moveDir += Vector3.forward;
            if (Input.GetKey(KeyCode.S)) moveDir -= Vector3.forward;
            if (Input.GetKey(KeyCode.D)) moveDir += Vector3.right;
            if (Input.GetKey(KeyCode.A)) moveDir -= Vector3.right;

            if (moveDir.sqrMagnitude > 0.001f)
            {
                moveDir.Normalize();

                Camera cam = Camera.main;
                if (cam != null)
                {
                    Vector3 camForward = cam.transform.forward;
                    Vector3 camRight = cam.transform.right;
                    camForward.y = 0f;
                    camRight.y = 0f;
                    camForward.Normalize();
                    camRight.Normalize();

                    Vector3 worldDir = camForward * moveDir.z + camRight * moveDir.x;
                    worldDir.Normalize();

                    float speed = moveSpeed * _bot.SpeedMultiplier * _bot.speedSliderValue;
                    _agent.Move(worldDir * speed * UnityEngine.Time.deltaTime);

                    Quaternion targetRot = Quaternion.LookRotation(worldDir, Vector3.up);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, rotationSpeed * UnityEngine.Time.deltaTime);
                }
            }
        }

        void HandleAiming()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
            if (groundPlane.Raycast(ray, out float enter))
            {
                _aimPosition = ray.GetPoint(enter);

                // 旋转机器人朝向瞄准点
                Vector3 lookDir = _aimPosition - transform.position;
                lookDir.y = 0f;
                if (lookDir.sqrMagnitude > 0.01f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, rotationSpeed * UnityEngine.Time.deltaTime);
                }
            }
        }

        // ============================================================
        // 进入/退出驾驶
        // ============================================================

        public void EnterPilot(GameObject pilot)
        {
            if (IsPiloting) return;
            if (pilot == null) return;
            if (_bot.IsDead) return;

            IsPiloting = true;
            Pilot = pilot;
            _bot.IsPiloted = true;
            _previousCommand = _bot.CurrentCommand;
            _bot.SetCommand(AIBotCommand.Pilot);

            ManualWeaponSlot = AttackPriority.Laser;

            Debug.Log($"[AIBotPilot] 玩家进入驾驶模式");
            EventBus.Publish(new AIBotPilotEnteredEvent(gameObject, pilot));
        }

        public void ExitPilot()
        {
            if (!IsPiloting) return;

            IsPiloting = false;
            _bot.IsPiloted = false;

            // 死亡时不恢复指令（Agent已禁用）
            if (!_bot.IsDead)
                _bot.SetCommand(_previousCommand);

            // 确保AI接管武器关闭
            if (_combat != null)
            {
                _combat.aiWeaponOverride = false;
                _combat.manualWeaponSlot = AttackPriority.Laser;
            }

            Debug.Log($"[AIBotPilot] 退出驾驶模式");
            EventBus.Publish(new AIBotPilotExitedEvent(gameObject, Pilot));

            Pilot = null;
        }

        // ============================================================
        // 武器循环 & 开火
        // ============================================================

        public void CycleManualWeapon()
        {
            // 节能模式下跳过激光
            if (!_bot.IsLaserEnabled)
            {
                ManualWeaponSlot = ManualWeaponSlot switch
                {
                    AttackPriority.RightArm => AttackPriority.LeftArm,
                    _ => AttackPriority.RightArm
                };
            }
            else
            {
                ManualWeaponSlot = ManualWeaponSlot switch
                {
                    AttackPriority.Laser => AttackPriority.RightArm,
                    AttackPriority.RightArm => AttackPriority.LeftArm,
                    AttackPriority.LeftArm => AttackPriority.Laser,
                    _ => AttackPriority.Laser
                };
            }

            if (_combat != null)
                _combat.manualWeaponSlot = ManualWeaponSlot;

            Debug.Log($"[AIBotPilot] 切换到: {ManualWeaponSlot} (combat={_combat != null})");
        }

        public void ManualFire()
        {
            Debug.Log($"[AIBotPilot] ManualFire 被调用, slot={ManualWeaponSlot}, combat={_combat != null}, shutdown={_bot.IsShutdown}");
            if (_combat == null || _bot.IsShutdown) return;

            if (ManualWeaponSlot == AttackPriority.Laser)
            {
                if (!_bot.IsLaserEnabled)
                {
                    Debug.Log("[AIBotPilot] 激光在节能模式下禁用");
                    return;
                }
                Debug.Log("[AIBotPilot] 激光开火");
                _combat.ManualFireLaser();
            }
            else if (ManualWeaponSlot == AttackPriority.RightArm)
            {
                if (_combat.CurrentRightArm == RightArmWeapon.None)
                {
                    Debug.Log("[AIBotPilot] 右臂未装备武器");
                    return;
                }
                Debug.Log($"[AIBotPilot] 右臂武器开火: {_combat.CurrentRightArm}");
                _combat.ManualFireAimed(_aimPosition);
            }
            else if (ManualWeaponSlot == AttackPriority.LeftArm)
            {
                if (_combat.CurrentLeftArm == LeftArmWeapon.None || _combat.CurrentLeftArm == LeftArmWeapon.Shield)
                {
                    Debug.Log("[AIBotPilot] 左臂未装备近战武器");
                    return;
                }
                Debug.Log($"[AIBotPilot] 左臂武器开火: {_combat.CurrentLeftArm}");
                _combat.ManualFireAimed(_aimPosition);
            }
        }

        // ============================================================
        // UI面板
        // ============================================================

        public void ToggleAIPanel()
        {
            if (AIBotUI.IsVisible)
                AIBotUI.Hide();
            else
                AIBotUI.Show(_bot);
        }

        // ============================================================
        // 武器名（供HUD显示）
        // ============================================================

        public string GetManualWeaponName()
        {
            return ManualWeaponSlot switch
            {
                AttackPriority.Laser => "激光",
                AttackPriority.RightArm => _combat != null ? WeaponDisplayName(_combat.CurrentRightArm) : "右臂",
                AttackPriority.LeftArm => _combat != null ? WeaponDisplayName(_combat.CurrentLeftArm) : "左臂",
                _ => "?"
            };
        }

        string WeaponDisplayName(RightArmWeapon w)
        {
            return w switch
            {
                RightArmWeapon.None => "右臂(空)",
                RightArmWeapon.Pistol => "手枪",
                RightArmWeapon.Rifle => "步枪",
                RightArmWeapon.Shotgun => "霰弹枪",
                RightArmWeapon.ElectromagneticRifle => "电磁步枪",
                _ => "?"
            };
        }

        string WeaponDisplayName(LeftArmWeapon w)
        {
            return w switch
            {
                LeftArmWeapon.None => "左臂(空)",
                LeftArmWeapon.Shield => "盾牌",
                LeftArmWeapon.Chainsaw => "电锯",
                LeftArmWeapon.Knife => "短刀",
                _ => "?"
            };
        }
    }
}
