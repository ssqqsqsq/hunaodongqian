using UnityEngine;

public class StaminaManager : MonoBehaviour
{
    public PlayerStateMachine sm;

    [Header("体力设置")]
    public float Max = 10f;
    public float ReleaseBuffer = 1f; // 缓冲期 1 秒

    public float Current { get; private set; }

    private float lastSprintExitTime = -999f;
    private bool inBuffer;

    void Awake()
    {
        Current = Max;
        // 监听状态机切换
        if (sm != null) sm.OnStateChanged += OnStateChanged;
    }

    void OnDestroy()
    {
        if (sm != null) sm.OnStateChanged -= OnStateChanged;
    }

    void OnStateChanged(PlayerState from, PlayerState to)
    {
        // 当从“冲刺”切换到“非冲刺”时，记录时间，开启缓冲
        if (from == PlayerState.Sprint && to != PlayerState.Sprint)
        {
            lastSprintExitTime = Time.time;
            inBuffer = true;
        }
        // 如果缓冲期内再次冲刺，立即取消缓冲
        if (to == PlayerState.Sprint)
        {
            inBuffer = false;
        }
    }

    void Update()
    {
        if (sm == null) return;

        // 检查缓冲期是否结束
        if (inBuffer && Time.time - lastSprintExitTime >= ReleaseBuffer)
        {
            inBuffer = false;
        }

        // 决定当前的体力变化速率
        float rate;
        if (inBuffer)
        {
            rate = 0f; // 缓冲期内，体力不变
        }
        else
        {
            rate = sm.CurrentData.StaminaRate; // 读取状态机给的速率
        }

        // 更新体力，并限制在 0~Max 之间
        Current = Mathf.Clamp(Current + rate * Time.deltaTime, 0f, Max);

        // 控制状态机的冲刺权限：体力 > 0 才能冲刺
        sm.CanSprint = Current > 0.01f;
    }
}