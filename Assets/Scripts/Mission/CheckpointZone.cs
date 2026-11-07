using System;
using UnityEngine;

// 打卡区：玩家进入后倒计时 10s。
// 倒计时结束 → 打卡成功并更新任务；中途离开 → 重置计时。
[RequireComponent(typeof(BoxCollider))]
public class CheckpointZone : TriggerZoneBase
{
    [Header("依赖（留空则自动在场景里找）")]
    [SerializeField] private MissionManager missionManager;

    [Header("打卡设置")]
    [Tooltip("需要停留的秒数")]
    [SerializeField] private float countdownDuration = 10f;
    [Tooltip("打卡成功后切换到的新任务")]
    [SerializeField] private MissionType nextMission = MissionType.Evacuation;
    [Tooltip("勾选后只有当前任务为「打卡」时才计时")]
    [SerializeField] private bool onlyWhenMissionIsCheckpoint = false;

    public event Action OnCountdownStart;
    public event Action<float> OnCountdownChanged; // 剩余秒数
    public event Action OnCountdownReset;
    public event Action OnCheckpointCompleted;

    public bool IsCounting { get; private set; }
    public float Remaining { get; private set; }
    public float Progress01 => countdownDuration <= 0f ? 1f : Mathf.Clamp01(1f - Remaining / countdownDuration);

    // 本轮是否已打卡（离开区域后可再次打卡）
    public bool Completed { get; private set; }

    protected override void Awake()
    {
        base.Awake();
        if (missionManager == null) missionManager = FindFirstObjectByType<MissionManager>();
    }

    private void Update()
    {
        if (!HasLivePlayerInside())
        {
            if (IsCounting) ResetCountdown();
            Completed = false;
            return;
        }

        if (Completed) return;

        if (onlyWhenMissionIsCheckpoint && missionManager != null && !missionManager.IsCheckpointMission)
        {
            if (IsCounting) ResetCountdown();
            return;
        }

        if (!IsCounting) StartCountdown();

        Remaining -= Time.deltaTime;
        OnCountdownChanged?.Invoke(Remaining);

        if (Remaining <= 0f) Complete();
    }

    private void StartCountdown()
    {
        Remaining = countdownDuration;
        IsCounting = true;
        if (verboseLog) Debug.Log($"[结算][打卡区] 开始打卡倒计时：{countdownDuration}s");
        OnCountdownStart?.Invoke();
    }

    private void ResetCountdown()
    {
        IsCounting = false;
        Remaining = 0f;
        if (verboseLog) Debug.Log("[结算][打卡区] 倒计时重置");
        OnCountdownReset?.Invoke();
    }

    private void Complete()
    {
        Completed = true;
        IsCounting = false;
        Remaining = 0f;
        if (verboseLog) Debug.Log($"[结算][打卡区] 打卡成功，下一个任务 = {nextMission}");
        OnCheckpointCompleted?.Invoke();

        if (missionManager == null)
        {
            Debug.LogWarning("CheckpointZone：缺少 MissionManager，打卡成功不会更新任务");
            return;
        }

        missionManager.CompleteCheckpoint(nextMission);
    }
}
