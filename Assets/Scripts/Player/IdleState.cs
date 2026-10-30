using UnityEngine.TextCore.Text;

public class IdleState : BaseState
{
    private readonly PlayerSM sm;

    public IdleState(PlayerSM stateMachine) : base(stateMachine)
    {
        sm = stateMachine;
    }

    public override void Enter()
    {
        // sm.anim.SetBool("Swim", false);
    }

    public override void UpdateState()
    {
        if (sm.IsDead)
        {
            sm.SwitchState(sm.deathState);
            return;
        }

        // 按住Shift直接进入冲刺
        if (sm.DashHeld)
        {
            sm.SwitchState(sm.dashState);
            return;
        }

        // 有移动输入，切游泳
        if (sm.HasMoveInput)
        {
            sm.SwitchState(sm.swimState);
        }
    }

    public override void FixedUpdateState()
    {
        // 待机：不施加推力、不转向
    }

    public override void Exit()
    {
    }
}
