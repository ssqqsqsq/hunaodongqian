using System;
using UnityEngine;

// 撤离区：第一次进入只登记、不判定；离开后再次进入才根据当前任务判定。
//   当前任务是撤离   → 5s 后过关成功；
//   当前任务不是撤离 → 10s 后打卡失败。
//   中途离开则重置倒计时。
[RequireComponent(typeof(BoxCollider))]
public class EvacuationZone : TriggerZoneBase
{
    [Header("依赖（留空则自动在场景里找）")]
    [SerializeField] private MissionManager missionManager;

    [Header("倒计时（秒）")]
    [Tooltip("当前任务是撤离时的倒计时")]
    [SerializeField] private float evacuateDuration = 5f;
    [Tooltip("当前任务不是撤离时的倒计时")]
    [SerializeField] private float nonEvacuateDuration = 10f;

    public event Action OnCountdownStart;
    public event Action<float> OnCountdownChanged; // 剩余秒数
    public event Action OnCountdownReset;
    public event Action OnCountdownFinished;

    // 是否已满足"离开过撤离区"的条件（之后再次进入才会判定）
    public bool CanJudge { get; private set; }
    public bool IsCounting { get; private set; }

    // 本次倒计时的总时长
    public float Duration { get; private set; }
    // 剩余秒数
    public float Remaining { get; private set; }
    public float Progress01 => Duration <= 0f ? 1f : Mathf.Clamp01(1f - Remaining / Duration);

    private bool finished;

    // 是否第一次进入过撤离区（第一次进入只登记，不判定）
    private bool enteredOnce;

    protected override void Awake()
    {
        base.Awake();
        if (missionManager == null) missionManager = FindFirstObjectByType<MissionManager>();
    }

    private void Update()
    {
        if (finished) return;

        if (!HasLivePlayerInside())
        {
            if (IsCounting) ResetCountdown();
            return;
        }

        // 只有离开过撤离区（再次进入）才根据当前任务判定
        if (!CanJudge)
        {
            if (IsCounting) ResetCountdown();
            return;
        }

        if (!IsCounting) StartCountdown();

        Remaining -= Time.deltaTime;
        OnCountdownChanged?.Invoke(Remaining);

        if (Remaining <= 0f) Finish();
    }

    // 第一次进入：只登记，不判定
    protected override void OnPlayerEnter(PlayerSM p)
    {
        if (!enteredOnce)
        {
            enteredOnce = true;
            if (verboseLog)
                Debug.Log("[结算][撤离区] 第一次进入：仅登记，离开后再进入才判定");
            return;
        }

        if (verboseLog)
            Debug.Log("[结算][撤离区] 再次进入撤离区，开始按当前任务判定");
    }

    // 第一次进入后离开：解锁判定条件
    protected override void OnPlayerExit(PlayerSM p)
    {
        if (verboseLog)
        {
            Vector3 pos = p != null ? p.transform.position : Vector3.zero;
            Debug.Log($"[结算][撤离区] 玩家离开撤离点：坐标 ({pos.x:0.0}, {pos.y:0.0})，" +
                      $"首次进入={enteredOnce}，可判定={CanJudge}，倒计时中={IsCounting}，已完成={finished}");
        }

        if (enteredOnce && !CanJudge)
        {
            CanJudge = true;
            if (verboseLog)
                Debug.Log("[结算][撤离区] 玩家离开撤离区，下次进入将按当前任务判定");
        }

        if (IsCounting) ResetCountdown();
    }

    private void StartCountdown()
    {
        Duration = IsEvacuationMission ? evacuateDuration : nonEvacuateDuration;
        Remaining = Duration;
        IsCounting = true;

        if (verboseLog)
            Debug.Log($"[结算][撤离区] 开始倒计时：{Duration}s（当前任务={(IsEvacuationMission ? "撤离" : "非撤离")}）");

        OnCountdownStart?.Invoke();
    }

    private void ResetCountdown()
    {
        IsCounting = false;
        Remaining = 0f;
        if (verboseLog) Debug.Log("[结算][撤离区] 倒计时重置");
        OnCountdownReset?.Invoke();
    }

    private void Finish()
    {
        finished = true;
        IsCounting = false;
        Remaining = 0f;

        if (verboseLog)
            Debug.Log($"[结算][撤离区] 倒计时结束，判定为：{(IsEvacuationMission ? "撤离成功" : "打卡失败")}");

        OnCountdownFinished?.Invoke();

        if (missionManager == null)
        {
            Debug.LogWarning("EvacuationZone：缺少 MissionManager，撤离结果不会更新任务");
            return;
        }

        if (IsEvacuationMission)
            missionManager.SucceedEvacuation();
        else
            missionManager.FailEvacuation();
    }

    private bool IsEvacuationMission => missionManager != null && missionManager.IsEvacuationMission;

    // 供任务系统或重开关卡时复位
    public void ResetZone()
    {
        finished = false;
        IsCounting = false;
        Remaining = 0f;
        CanJudge = false;
        enteredOnce = false;
    }
}
