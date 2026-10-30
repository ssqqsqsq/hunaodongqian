using System;
using UnityEngine;

public class PlayerSM : StateMachineBase
{
    public event Action<BaseState> OnStateChanged;
    public event Action OnDeath;
    [Header("移动参数")]
    public float swimForce = 9f;
    public float maxSpeed = 6f;

    [Header("转向参数")]
    public float rotateSpeed = 240f;
    public float rotationOffset = 90f;
    public float maxTilt = 80f;
    public bool flipAngle = true;
    public bool flipMove = true;
    public bool invertZ = true;

    [Header("冲刺参数")]
    public float sprintForceMultiplier = 1.6f;
    public float sprintSpeedMultiplier = 2f;
    public float dashDuration = 0.25f;

    [Header("状态实例")]
    public IdleState idleState;
    public SwimState swimState;
    public DashState dashState;
    public DeathState deathState;

    [HideInInspector] public Rigidbody rb;
    [HideInInspector] public float facingSign = 1f; // 角色朝向，跨状态保留

    // 统一缓存输入
    public Vector2 MoveDir { get; private set; }
    public bool HasMoveInput { get; private set; }
    public bool DashDown { get; private set; }
    public bool DashHeld { get; private set; }
    public bool IsDead { get; set; } = false;
    private BaseState currentState;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.rotation = Quaternion.Euler(0f, 0f, rotationOffset);

        // 初始化所有状态，把自身传进去
        idleState = new IdleState(this);
        swimState = new SwimState(this);
        dashState = new DashState(this);
        deathState = new DeathState(this);
    }

    private void Start()
    {
        SwitchState(idleState);
    }

    private void Update()
    {
        // 输入统一在Update读，避免FixedUpdate丢帧
        ReadInput();
        currentState?.UpdateState();
    }

    private void FixedUpdate()
    {
        currentState?.FixedUpdateState();

        // 通用约束：所有状态都锁Z轴、锁X轴角速度
        transform.position = new Vector3(rb.position.x, rb.position.y, 0f);
        rb.angularVelocity = new Vector3(0f, rb.angularVelocity.y, rb.angularVelocity.z);
    }
    //检测输入
    private void ReadInput()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        MoveDir = new Vector2(h, v).normalized;
        HasMoveInput = MoveDir.sqrMagnitude > 0.05f;
        DashHeld = Input.GetKey(KeyCode.LeftShift);
    }
    // 切换状态
    public void SwitchState(BaseState next)
    {
        currentState?.Exit();
        currentState = next;
        currentState.Enter();
        OnStateChanged?.Invoke(currentState);
    }

    // 通用工具：限制平面速度
    public void ClampPlaneVelocity(float maxSpeed)
    {
        Vector3 pv = new Vector3(rb.velocity.x, rb.velocity.y, 0f);
        if (pv.magnitude > maxSpeed)
            pv = pv.normalized * maxSpeed;
        rb.velocity = new Vector3(pv.x, pv.y, rb.velocity.z);
    }
    //死
    private void Die()
    {
        IsDead = true;
        SwitchState(deathState); // 强制切到死亡状态
        OnDeath?.Invoke();       // 广播死亡事件
    }
    //是我，杀了我
    public void Kill()
    {
        if (IsDead) return;
        Die();
    }
}
