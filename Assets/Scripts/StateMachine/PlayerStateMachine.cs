using System;
using UnityEngine;

public class PlayerStateMachine : MonoBehaviour
{
    [Header("引用")]
    public Transform body;             // 槽位会在这里出现
    public SideScrollerSwim movement;  // 槽位会在这里出现

    [Header("阈值")]
    public float moveInputThreshold = 0.05f;

    public PlayerState Current { get; private set; } = PlayerState.Idle;
    public IPlayerState CurrentData { get; private set; } = new IdleState();
    public bool IsDead { get; private set; }
    public bool CanSprint { get; set; } = true;

    public event Action<PlayerState, PlayerState> OnStateChanged;

    private readonly IPlayerState idle = new IdleState();
    private readonly IPlayerState swim = new SwimState();
    private readonly IPlayerState sprint = new SprintState();
    private readonly IPlayerState dead = new DeadState();

    void Awake()
    {
        if (body == null && transform.childCount > 0)
            body = transform.GetChild(0);

        if (movement == null && body != null)
            movement = body.GetComponent<SideScrollerSwim>();
    }

    void Update()
    {
        if (IsDead) return;

        bool hasMove = movement != null && movement.HasMoveInput(moveInputThreshold);
        bool wantSprint = Input.GetKey(KeyCode.LeftShift) && hasMove && CanSprint;

        PlayerState next;
        if (wantSprint) next = PlayerState.Sprint;
        else if (hasMove) next = PlayerState.Swim;
        else next = PlayerState.Idle;

        SwitchTo(next);
    }

    public void MarkDead()
    {
        IsDead = true;
        SwitchTo(PlayerState.Dead);
    }

    void SwitchTo(PlayerState next)
    {
        if (next == Current) return;

        var from = Current;
        Current = next;
        CurrentData = next switch
        {
            PlayerState.Dead => dead,
            PlayerState.Idle => idle,
            PlayerState.Swim => swim,
            PlayerState.Sprint => sprint,
            _ => idle
        };

        // 同步冲刺状态给移动脚本
        if (movement != null)
            movement.IsSprinting = (next == PlayerState.Sprint);

        OnStateChanged?.Invoke(from, next);
        Debug.Log($"[State] {from} -> {next}");
    }
}