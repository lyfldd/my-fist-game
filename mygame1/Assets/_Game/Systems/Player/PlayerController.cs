using UnityEngine;
using _Game.Systems.Character;
using _Game.Systems.PlayerInput;
using _Game.Systems.Audio;
using _Game.Core;
using Inv = _Game.Systems.Inventory.Inventory;

namespace _Game.Systems.Player
{
public class PlayerController : MonoBehaviour
{
    [Header("移动参数")]
    public float moveSpeed = GameConstants.PLAYER_MOVE_SPEED;
    public float runSpeedMultiplier = 1.6f;
    public float maxOverloadSlow = GameConstants.PLAYER_MAX_OVERLOAD_SLOW;
    public float rotationSpeed = GameConstants.PLAYER_ROTATION_SMOOTH_SPEED;

    [Header("动画平滑")]
    public float animAcceleration = 3f;   // 加速时 Speed 参数上升速度
    public float animDeceleration = 5f;   // 减速/松手时 Speed 参数下降速度

    private Rigidbody _rb;
    private Inv _inventory;
    private PlayerCharacter _playerCharacter;
    private StaminaSystem _stamina;
    private MouseGroundProjector _projector;
    private Animator _animator;
    private _Game.Systems.Weapon.WeaponAiming _weaponAiming;

    private float _smoothedSpeedRatio;  // 平滑后的 Speed 参数值，避免瞬间跳变

    private const string FootstepKey = "player_footstep";

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _inventory = GetComponent<Inv>();
        _playerCharacter = GetComponent<PlayerCharacter>();
        _stamina = GetComponent<StaminaSystem>();
        _projector = GetComponent<MouseGroundProjector>();
        _animator = GetComponentInChildren<Animator>();
        _weaponAiming = GetComponent<_Game.Systems.Weapon.WeaponAiming>();
    }

    void Update()
    {
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveZ = Input.GetAxisRaw("Vertical");
        Vector3 moveInput = new Vector3(moveX, 0, moveZ).normalized;
        bool isMoving = moveInput.sqrMagnitude > 0.01f;

        bool wantsRun = Input.GetKey(KeyCode.LeftShift) && isMoving;

        // 转向：移动时朝移动方向，静止时朝鼠标
        if (isMoving)
        {
            // 朝移动方向转
            Quaternion targetRotation = Quaternion.LookRotation(moveInput, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, targetRotation, rotationSpeed * 2f * UnityEngine.Time.deltaTime);
        }
        else if (_projector != null && _projector.HasValidTarget)
        {
            // 静止时朝鼠标（瞄准用）
            Vector3 toTarget = _projector.GroundPoint - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, targetRotation, rotationSpeed * UnityEngine.Time.deltaTime);
            }
        }
        // 体力状态: 跑/走/静止
        if (_stamina != null)
        {
            bool actuallyRunning = wantsRun && !_stamina.IsExhausted;
            _stamina.SetRunning(actuallyRunning);
            _stamina.SetWalking(isMoving && !actuallyRunning);
        }

        // 计算当前实际速度
        float currentSpeed = moveSpeed;

        // 跑步加速
        if (wantsRun && _stamina != null && !_stamina.IsExhausted)
            currentSpeed *= runSpeedMultiplier;

        // 体力耗尽减速
        if (_stamina != null && _stamina.IsExhausted)
            currentSpeed *= GameConstants.STAMINA_EXHAUSTED_SPEED_MULT;

        if (_inventory != null && _inventory.IsOverloaded)
            currentSpeed *= (1f - _inventory.OverloadRatio * maxOverloadSlow);

        if (_playerCharacter != null)
            currentSpeed *= _playerCharacter.GetMoveSpeedModifier();

        // 右键瞄准减速
        if (_weaponAiming != null && _weaponAiming.IsAimingDownSights)
            currentSpeed *= GameConstants.AIM_MOVE_SPEED_MULTIPLIER;

        // 动画：归一化速度平滑后传给 1D BlendTree
        // 角色已在上面朝移动方向旋转，动画只需控制速度（0=Idle, 1=Walk, 1.5=SlowRun, 2=FastRun）
        float targetRatio = moveInput.sqrMagnitude > 0.01f ? currentSpeed / moveSpeed : 0f;

        // 加速用较慢的上升，减速用较快的下降（更跟手）
        float smoothSpeed = targetRatio > _smoothedSpeedRatio ? animAcceleration : animDeceleration;
        _smoothedSpeedRatio = Mathf.MoveTowards(
            _smoothedSpeedRatio, targetRatio, smoothSpeed * UnityEngine.Time.deltaTime);

        if (_animator != null)
            _animator.SetFloat("Speed", _smoothedSpeedRatio);

        // 应用移动
        Vector3 velocity = moveInput * currentSpeed;
        velocity.y = _rb.velocity.y;
        _rb.velocity = velocity;

        // 脚步声
        if (isMoving)
            SoundEmitter.SetFootstep(FootstepKey, transform.position, wantsRun);
        else
            SoundEmitter.StopFootstep(FootstepKey);
    }

    void OnDestroy()
    {
        SoundEmitter.StopFootstep(FootstepKey);
    }
}
}
