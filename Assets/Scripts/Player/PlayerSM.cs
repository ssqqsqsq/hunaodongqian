using System;
using System.Collections.Generic;
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

    [Header("鼠标瞄准")]
    [Tooltip("留空则自动取 Camera.main")]
    public Camera worldCamera;
    public bool allowMouseAim = true;
    [Tooltip("光标离角色多近时不再判定方向（世界单位）。约等于\"停到离光标多近\"")]
    public float aimDeadZone = 0.5f;

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

    // Debuff「强制疾跑」：等价于一直按住 Shift（由 DebuffManager 设置）
    public bool ForceSprint { get; set; }
    public bool IsDead { get; set; } = false;

    // QTE / 剧情强制锁定：锁定期间忽略全部输入（由 CanalQTEController 设置）
    public bool ControlLocked { get; set; }

    // ControlLocked 期间由外部设置：把身体对齐到该方向（产道 QTE 用）
    public Vector2? ForcedFacingDir { get; set; }

    // ---- Debuff 操作类效果（由 EffectController 写入） ----
    public bool InputInvert;      // 效果8 操作反向：移动 / 鼠标朝向 180° 反转
    public bool RandomDrift;      // 效果9 随机偏移：移动中周期性横向漂移
    public float InputDelayTime;  // 效果7 操作延迟：>0 时输入延迟这么多秒才响应

    [Header("随机偏移（效果9）")]
    [Tooltip("横向漂移持续时长（秒）")]
    public float driftDuration = 0.5f;
    [Tooltip("漂移触发间隔（秒，随机范围）")]
    public float driftIntervalMin = 4f;
    public float driftIntervalMax = 6f;

    // ---- 鼠标瞄准 ----
    public bool AimHeld { get; private set; }        // 按住右键：朝向跟随鼠标
    public bool MouseMoveHeld { get; private set; }  // 右键 + 左键：朝鼠标方向移动
    public Vector2 AimDir { get; private set; }      // 世界空间、已归一化（Z 已消除）

    private BaseState currentState;
    private Camera cachedCamera;

    // 随机偏移（效果9）：周期性给一个垂直于游动方向的力
    private float driftTimer;     // 距下次漂移的间隔倒计时
    private Vector2 driftDir;     // 当前漂移方向（生效中）
    private float driftRemaining; // 漂移剩余时长

    // 操作延迟（效果7）：记录原始输入帧，读取“延迟前”那一帧作为生效输入
    private struct InputFrame
    {
        public float time;
        public Vector2 keyboard;
        public Vector2 aim;
        public bool aimHeld;
        public bool mouseMove;
        public bool dash;
    }

    private readonly Queue<InputFrame> inputHistory = new Queue<InputFrame>();

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
        UpdateRandomDrift();
        currentState?.UpdateState();
    }

    private void FixedUpdate()
    {
        currentState?.FixedUpdateState();

        // QTE 强制朝向优先：按判定区方向把身体对齐到与产道平行
        if (ControlLocked && ForcedFacingDir.HasValue)
        {
            ForceFacing(ForcedFacingDir.Value);
        }

        // 通用约束：所有状态都锁Z轴、锁X轴角速度
        transform.position = new Vector3(rb.position.x, rb.position.y, 0f);
        rb.angularVelocity = new Vector3(0f, rb.angularVelocity.y, rb.angularVelocity.z);
    }
    //检测输入
    private void ReadInput()
    {
        // QTE 强制锁定：忽略全部输入，角色冻结（状态机会自然回到待机，不施加任何推力）
        if (ControlLocked)
        {
            MoveDir = Vector2.zero;
            HasMoveInput = false;
            DashHeld = false;
            AimHeld = false;
            MouseMoveHeld = false;
            AimDir = Vector2.zero;
            return;
        }

        // 键盘方向
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector2 keyboardDir = new Vector2(h, v).normalized;

        // 鼠标：按住右键 = 瞄准（朝向跟鼠标）；再按住左键 = 朝鼠标移动
        AimHeld = allowMouseAim && Input.GetMouseButton(1);
        MouseMoveHeld = AimHeld && Input.GetMouseButton(0);
        AimDir = AimHeld ? ReadAimDir() : Vector2.zero;
        DashHeld = Input.GetKey(KeyCode.LeftShift) || ForceSprint;

        // 操作反向：移动方向与鼠标朝向 180° 反转
        if (InputInvert)
        {
            keyboardDir = -keyboardDir;
            AimDir = -AimDir;
        }

        // 操作延迟：把原始输入入队，生效输入 = "延迟前"那一帧（按下与松开都延迟）
        if (InputDelayTime > 0f)
        {
            inputHistory.Enqueue(new InputFrame
            {
                time = Time.time,
                keyboard = keyboardDir,
                aim = AimDir,
                aimHeld = AimHeld,
                mouseMove = MouseMoveHeld,
                dash = DashHeld,
            });

            float target = Time.time - InputDelayTime;
            InputFrame effective = inputHistory.Peek();
            foreach (InputFrame f in inputHistory)
            {
                if (f.time <= target) effective = f;
            }

            ApplyFrame(effective);

            // 清理远早于延迟窗的旧数据，防止队列无限增长
            float expire = Time.time - InputDelayTime - 1f;
            while (inputHistory.Count > 1 && inputHistory.Peek().time <= expire)
                inputHistory.Dequeue();
        }
        else
        {
            inputHistory.Clear();
            ApplyFrame(new InputFrame
            {
                time = Time.time,
                keyboard = keyboardDir,
                aim = AimDir,
                aimHeld = AimHeld,
                mouseMove = MouseMoveHeld,
                dash = DashHeld,
            });
        }
    }

    // 把一帧输入写进对外属性
    private void ApplyFrame(InputFrame f)
    {
        // 右键+左键时鼠标方向覆盖键盘方向，否则还是键盘
        MoveDir = f.mouseMove && f.aim.sqrMagnitude > 0f ? f.aim : f.keyboard;
        HasMoveInput = MoveDir.sqrMagnitude > 0.05f;
        DashHeld = f.dash;
        AimHeld = f.aimHeld;
        MouseMoveHeld = f.mouseMove;
        AimDir = f.aim;
    }

    // 随机偏移（效果9）：移动中每 2~5s 给一个垂直于游动方向的力，持续 0.5s，
    // 同时改变移动方向与朝向。不在移动时不生效。
    private void UpdateRandomDrift()
    {
        if (!RandomDrift || !HasMoveInput)
        {
            driftTimer = 0f;
            driftDir = Vector2.zero;
            driftRemaining = 0f;
            return;
        }

        if (driftRemaining > 0f)
        {
            // 漂移生效中：递减剩余时间
            driftRemaining -= Time.deltaTime;
            if (driftRemaining <= 0f)
            {
                driftDir = Vector2.zero;
                driftTimer = UnityEngine.Random.Range(driftIntervalMin, driftIntervalMax);
            }
            return;
        }

        // 等待下一次漂移
        driftTimer -= Time.deltaTime;
        if (driftTimer <= 0f)
        {
            Vector2 dir = MoveDir.sqrMagnitude > 0.01f ? MoveDir.normalized : Vector2.right;
            Vector2 perp = new Vector2(-dir.y, dir.x); // 垂直于游动方向
            if (UnityEngine.Random.value < 0.5f) perp = -perp;

            driftDir = perp.normalized;
            driftRemaining = driftDuration;
        }
    }

    // 当前应施力的方向：漂移生效时用漂移方向，否则用玩家输入
    public Vector2 GetEffectiveMoveDir()
    {
        return driftRemaining > 0f ? driftDir : MoveDir;
    }

    // 世界空间里"角色 -> 鼠标"的单位方向；Z 已归零
    private Vector2 ReadAimDir()
    {
        // 用私有缓存取 Camera.main，避免写回序列化字段把场景标记为脏
        Camera cam = worldCamera != null ? worldCamera : cachedCamera;
        if (cam == null)
        {
            cachedCamera = Camera.main;
            cam = cachedCamera;
        }

        if (cam == null)
        {
            Debug.LogWarning("PlayerSM：找不到相机，鼠标瞄准不可用（可在 Inspector 上绑定 worldCamera）");
            return Vector2.zero;
        }

        // 透视相机必须给一个深度，取相机到角色所在平面的距离
        float depth = Mathf.Abs(transform.position.z - cam.transform.position.z);
        if (depth < cam.nearClipPlane)
        {
            depth = cam.nearClipPlane + 0.01f;
        }

        Vector3 screenPoint = Input.mousePosition;
        screenPoint.z = depth;
        Vector3 worldPoint = cam.ScreenToWorldPoint(screenPoint);

        Vector2 dir = new Vector2(
            worldPoint.x - transform.position.x,
            worldPoint.y - transform.position.y
        );

        // 光标压在角色身上时方向会剧烈抖动，直接忽略
        if (dir.sqrMagnitude < aimDeadZone * aimDeadZone)
        {
            return Vector2.zero;
        }

        return dir.normalized;
    }

    // 当前该朝哪看：漂移中跟漂移方向，否则按住右键优先跟鼠标，再否则跟移动方向
    public bool TryGetFacingDir(out Vector2 dir)
    {
        // 漂移生效时，朝向跟随漂移方向（覆盖鼠标与移动）
        if (driftRemaining > 0f && driftDir.sqrMagnitude > 0f)
        {
            dir = driftDir;
            return true;
        }

        if (AimHeld && AimDir.sqrMagnitude > 0f)
        {
            dir = AimDir;
            return true;
        }

        if (HasMoveInput)
        {
            dir = MoveDir;
            return true;
        }

        dir = Vector2.zero;
        return false;
    }

    // 统一的转向逻辑：原来 SwimState / DashState 各复制了一份，现在收到这里
    public void ApplyFacing(Vector2 dir)
    {
        ApplyFacingInternal(dir);
    }

    // QTE 等强制对齐用：与 ApplyFacing 同一套映射
    public void ForceFacing(Vector2 dir)
    {
        ApplyFacingInternal(dir);
    }

    private void ApplyFacingInternal(Vector2 dir)
    {
        if (dir.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Vector2 faceDir = new Vector2(
            flipAngle ? -dir.x : dir.x,
            dir.y
        );
        if (faceDir.sqrMagnitude > 1f) faceDir.Normalize();

        // 更新朝向翻转
        Vector2 rel = new Vector2(faceDir.x * facingSign, faceDir.y);
        if (rel.x < -0.2f)
            facingSign = -facingSign;

        rel = new Vector2(faceDir.x * facingSign, faceDir.y);
        float tilt = Mathf.Atan2(rel.y, rel.x) * Mathf.Rad2Deg;
        tilt = Mathf.Clamp(tilt, -maxTilt, maxTilt);

        float yaw = facingSign > 0f ? 0f : 180f;
        float z = rotationOffset - tilt;
        if (invertZ) z = -z;

        Quaternion targetRot = Quaternion.Euler(0f, yaw, z);
        rb.rotation = Quaternion.RotateTowards(
            rb.rotation, targetRot,
            rotateSpeed * Time.fixedDeltaTime
        );
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
