using System;
using System.Collections.Generic;
using UnityEngine;

// 单局统计：本关总用时、到达的最大深度、收集品列表、是否完成过打卡。
// 结算界面从这里取数据；收集品通过 AddCollectible 登记。
public class LevelStats : MonoBehaviour
{
    [Header("依赖（留空自动在场景里找）")]
    [SerializeField] private DepthComponent depthComponent;
    [SerializeField] private MissionManager missionManager;

    [Header("计时")]
    [Tooltip("进入关卡后自动开始计时")]
    [SerializeField] private bool startTimerOnStart = true;

    [Header("调试")]
    [Tooltip("打印统计相关日志")]
    [SerializeField] private bool verboseLog = true;

    // 已过去的秒数（结算时会停止累加）
    public float ElapsedTime { get; private set; }

    // 本关到达的最大深度
    public float MaxDepth { get; private set; }

    // 本关是否完成过打卡（结算时用来区分「打卡成功」与「仓皇逃离」）
    public bool CheckpointCompleted { get; private set; }

    // 已收集的收集品（按收集顺序）
    public IReadOnlyList<CollectibleData> Collected => collected;

    // 新收集品入账
    public event Action<CollectibleData> OnCollectibleAdded;

    private readonly List<CollectibleData> collected = new List<CollectibleData>();
    private bool timing;
    private float logTimer;

    private void Awake()
    {
        if (depthComponent == null) depthComponent = FindFirstObjectByType<DepthComponent>();
        if (missionManager == null) missionManager = FindFirstObjectByType<MissionManager>();

        if (verboseLog)
            Debug.Log($"[结算][LevelStats] 初始化：depthComponent={(depthComponent != null)}, missionManager={(missionManager != null)}, 自动计时={startTimerOnStart}");
    }

    private void OnEnable()
    {
        if (missionManager != null) missionManager.OnCheckpointCompleted += HandleCheckpointCompleted;
        else if (verboseLog) Debug.LogWarning("[结算][LevelStats] 没有 MissionManager，无法记录打卡完成状态");
    }

    private void OnDisable()
    {
        if (missionManager != null) missionManager.OnCheckpointCompleted -= HandleCheckpointCompleted;
    }

    private void Start()
    {
        if (startTimerOnStart) StartTimer();
    }

    private void Update()
    {
        if (timing)
        {
            ElapsedTime += Time.deltaTime;
            logTimer += Time.deltaTime;

            if (verboseLog && logTimer >= 5f)
            {
                logTimer = 0f;
                Debug.Log($"[结算][LevelStats] 用时 {ElapsedTime:0.0}s，最大深度 {MaxDepth:0.0}m");
            }
        }

        // 深度组件按阈值广播，这里直接取当前值，够用且简单
        if (depthComponent != null && depthComponent.CurrentDepth > MaxDepth)
            MaxDepth = depthComponent.CurrentDepth;
    }

    public void StartTimer()
    {
        timing = true;
        if (verboseLog) Debug.Log("[结算][LevelStats] 计时开始");
    }

    public void StopTimer()
    {
        timing = false;
        if (verboseLog) Debug.Log($"[结算][LevelStats] 计时停止于 {ElapsedTime:0.0}s");
    }

    public void ResetStats()
    {
        ElapsedTime = 0f;
        MaxDepth = 0f;
        CheckpointCompleted = false;
        collected.Clear();
    }

    // 收集品统一入口（别的系统也可以直接往这登记）
    public void AddCollectible(CollectibleData data)
    {
        if (data == null) return;
        collected.Add(data);
        if (verboseLog) Debug.Log($"[结算][LevelStats] 收集品入账：{data.displayName} ({data.id})，当前共 {collected.Count} 个");
        OnCollectibleAdded?.Invoke(data);
    }

    public void MarkCheckpointCompleted()
    {
        CheckpointCompleted = true;
    }

    private void HandleCheckpointCompleted()
    {
        CheckpointCompleted = true;
        if (verboseLog) Debug.Log("[结算][LevelStats] 收到打卡完成事件，本局已打卡");
    }
}
