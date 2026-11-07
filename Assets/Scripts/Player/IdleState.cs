using UnityEngine;
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

        // 只有移动时才切换；没移动就保持静止（按住 Shift 没动不进入冲刺）
        if (sm.HasMoveInput)
        {
            sm.SwitchState(sm.DashHeld ? sm.dashState : sm.swimState);
        }
    }

    public override void FixedUpdateState()
    {
        // 待机不施加推力，但按住右键时仍然要跟着鼠标转身
        if (sm.TryGetFacingDir(out Vector2 faceDir))
        {
            sm.ApplyFacing(faceDir);
        }
    }

    public override void Exit()
    {
    }
}
