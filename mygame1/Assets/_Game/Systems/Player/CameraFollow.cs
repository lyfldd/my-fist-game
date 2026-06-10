using UnityEngine;
using _Game.Core;

public class CameraFollow : MonoBehaviour
{
    [Header("����Ŀ��")]
    public Transform target;

    [Header("���������")]
    public float distance = GameConstants.CAMERA_DISTANCE;   // ��Ŀ��ľ���
    public float height = GameConstants.CAMERA_HEIGHT;       // ������߶�
    public float angle = GameConstants.CAMERA_ANGLE;         // ���ӽǶȣ��ȣ�0=ˮƽ��90=��ֱ���ӣ�
    public float smoothSpeed = GameConstants.CAMERA_SMOOTH_SPEED; // ����ƽ����

    // ����ʱ״̬
    private Transform _defaultTarget;
    private float _defaultDistance;
    private float _currentDistance;
    private bool _isFollowingVehicle;

    void Start()
    {
        // Start 里拿 target（GameBootstrap 在 Awake 之后才设 target）
        if (target == null)
            target = PlayerRegistry.Transform;
        _defaultTarget = target;
        _defaultDistance = distance;
        _currentDistance = distance;
    }

    void OnEnable()
    {
        EventBus.Subscribe<VehicleEnteredEvent>(OnVehicleEntered);
        EventBus.Subscribe<VehicleExitedEvent>(OnVehicleExited);
        EventBus.Subscribe<AIBotPilotEnteredEvent>(OnPilotEntered);
        EventBus.Subscribe<AIBotPilotExitedEvent>(OnPilotExited);
    }

    void OnDestroy()
    {
        EventBus.Unsubscribe<VehicleEnteredEvent>(OnVehicleEntered);
        EventBus.Unsubscribe<VehicleExitedEvent>(OnVehicleExited);
        EventBus.Unsubscribe<AIBotPilotEnteredEvent>(OnPilotEntered);
        EventBus.Unsubscribe<AIBotPilotExitedEvent>(OnPilotExited);
    }

    void LateUpdate()
    {
        if (target == null)
        {
            Debug.LogWarning("CameraFollow: û�����ø���Ŀ�꣡");
            return;
        }

        // �� angle ���������ƫ��
        float rad = angle * Mathf.Deg2Rad;
        Vector3 targetPosition = target.position;
        targetPosition += new Vector3(0, height, 0);                    // ����̧��
        targetPosition -= new Vector3(0, 0, _currentDistance);          // �����

        // ƽ���ƶ�
        transform.position = Vector3.Lerp(
            transform.position,
            targetPosition,
            smoothSpeed * UnityEngine.Time.deltaTime
        );

        // ����Ŀ�꣨��׼�ؿ�λ�ã�
        transform.LookAt(target.position + Vector3.up * GameConstants.PLAYER_LOOK_AT_CHEST);
    }

    // ============================================================
    // ����Ŀ���л�
    // ============================================================

    private void OnVehicleEntered(VehicleEnteredEvent evt)
    {
        if (evt.Vehicle != null)
        {
            target = evt.Vehicle.transform;
            _currentDistance = _defaultDistance + GameConstants.VEHICLE_CAMERA_EXTRA_DISTANCE;
            _isFollowingVehicle = true;
        }
    }

    private void OnVehicleExited(VehicleExitedEvent evt)
    {
        target = _defaultTarget ?? PlayerRegistry.Transform;
        _currentDistance = _defaultDistance;
        _isFollowingVehicle = false;
    }

    private void OnPilotEntered(AIBotPilotEnteredEvent evt)
    {
        if (evt.Bot != null)
        {
            target = evt.Bot.transform;
            _currentDistance = _defaultDistance + 3f;
        }
    }

    private void OnPilotExited(AIBotPilotExitedEvent evt)
    {
        target = _defaultTarget ?? PlayerRegistry.Transform;
        _currentDistance = _defaultDistance;
    }
}
