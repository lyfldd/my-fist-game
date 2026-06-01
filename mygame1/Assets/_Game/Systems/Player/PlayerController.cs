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

    private Rigidbody _rb;
    private Inv _inventory;
    private PlayerCharacter _playerCharacter;
    private StaminaSystem _stamina;
    private MouseGroundProjector _projector;
    private Animator _animator;

    private const string FootstepKey = "player_footstep";

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _inventory = GetComponent<Inv>();
        _playerCharacter = GetComponent<PlayerCharacter>();
        _stamina = GetComponent<StaminaSystem>();
        _projector = GetComponent<MouseGroundProjector>();
        _animator = GetComponent<Animator>();
    }

    void Update()
    {
        // 角色始终面朝鼠标
        if (_projector != null && _projector.HasValidTarget)
        {
            Vector3 toTarget = _projector.GroundPoint - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, targetRotation, rotationSpeed * UnityEngine.Time.deltaTime);
            }
        }

        float moveX = Input.GetAxisRaw("Horizontal");
        float moveZ = Input.GetAxisRaw("Vertical");
        Vector3 moveInput = new Vector3(moveX, 0, moveZ).normalized;

        bool wantsRun = Input.GetKey(KeyCode.LeftShift) && moveInput.sqrMagnitude > 0.01f;
        bool isMoving = moveInput.sqrMagnitude > 0.01f;

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

        // 动画：将归一化速度传给 Animator Blend Tree（1=Walk, 1.6=Run）
        if (_animator != null)
            _animator.SetFloat("Speed", moveInput.sqrMagnitude > 0.01f ? currentSpeed / moveSpeed : 0f);

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
