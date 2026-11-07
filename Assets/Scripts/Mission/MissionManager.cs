using System;
using UnityEngine;

// 当前任务类型
public enum MissionType
{
    None = 0,        // 自由探索
    Checkpoint = 1,  // 前往打卡区
    Evacuation = 2,  // 前往撤离区
}

// 任务状态中枢：撤离区 / 打卡区据此决定行为，结果通过事件广播。
// 关卡逻辑或后续任务系统在运行时调用 SetMission / CompleteCheckpoint 等推进任务。
public class MissionManager : MonoBehaviour
{
    [Header("初始任务")]
    [SerializeField] private MissionType currentMission = MissionType.None;

    [Header("调试")]
    [SerializeField] private bool verboseLog = true;

    public MissionType CurrentMission => currentMission;
    public bool IsCheckpointMission => currentMission == MissionType.Checkpoint;
    public bool IsEvacuationMission => currentMission == MissionType.Evacuation;

    // 任务切换（旧任务, 新任务）
    public event Action<MissionType, MissionType> OnMissionChanged;
    // 打卡成功
    public event Action OnCheckpointCompleted;
    // 撤离成功（过关）
    public event Action OnEvacuationSucceeded;
    // 非撤离任务时进入撤离区：打卡失败
    public event Action OnEvacuationFailed;

    private void Awake()
    {
        MissionManager[] all = FindObjectsByType<MissionManager>(FindObjectsSortMode.None);
        if (all.Length > 1)
            Debug.LogWarning($"MissionManager：场景里有 {all.Length} 个，请只保留一个");

        if (verboseLog)
            Debug.Log($"[结算][任务] 初始化，当前任务 = {currentMission}");
    }

    public void SetMission(MissionType mission)
    {
        if (currentMission == mission) return;

        MissionType old = currentMission;
        currentMission = mission;

        if (verboseLog)
            Debug.Log($"[结算][任务] 任务切换：{old} → {mission}");

        OnMissionChanged?.Invoke(old, mission);
    }

    // 打卡成功：广播后推进到下一个任务
    public void CompleteCheckpoint(MissionType nextMission = MissionType.Evacuation)
    {
        if (verboseLog)
            Debug.Log($"[结算][任务] 打卡成功，下一个任务 = {nextMission}");

        OnCheckpointCompleted?.Invoke();
        SetMission(nextMission);
    }

    // 撤离成功：过关（结算界面等由监听者处理）
    public void SucceedEvacuation()
    {
        if (verboseLog)
            Debug.Log("[结算][任务] 撤离成功 → 过关");

        OnEvacuationSucceeded?.Invoke();
        SetMission(MissionType.None);
    }

    // 打卡失败：当前不是撤离任务却进入了撤离区
    public void FailEvacuation()
    {
        if (verboseLog)
            Debug.Log("[结算][任务] 非撤离任务进入撤离区 → 打卡失败");

        OnEvacuationFailed?.Invoke();
        SetMission(MissionType.None);
    }
}
