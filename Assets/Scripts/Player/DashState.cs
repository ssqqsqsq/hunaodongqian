using UnityEngine;
using UnityEngine.TextCore.Text;

public class DashState : BaseState
{
    private readonly PlayerSM sm;

    public DashState(PlayerSM stateMachine) : base(stateMachine)
    {
        sm = stateMachine;
    }

    public override void Enter()
    {
        //// 进入时给一小段初始冲量，起步顺滑，和你原来的手感对齐
        //Vector2 dashDir = sm.HasMoveInput
        //    ? sm.MoveDir
        //    : new Vector2(sm.rb.velocity.x, sm.rb.velocity.y).normalized;

        //if (dashDir.sqrMagnitude < 0.01f)
        //    dashDir = new Vector2(sm.facingSign, 0f);

        //sm.rb.AddForce(
        //    new Vector3(dashDir.x, dashDir.y, 0f) * sm.swimForce * sm.sprintForceMultiplier * 0.5f,
        //    ForceMode.Impulse
        //);
    }

    public override void UpdateState()
    {
        // 死亡优先
        if (sm.IsDead)
        {
            sm.SwitchState(sm.deathState);
            return;
        }

        // 没有移动 → 静止（按住 Shift 也不冲刺）
        if (!sm.HasMoveInput)
        {
            sm.SwitchState(sm.idleState);
            return;
        }

        // 松开 Shift → 回到游泳
        if (!sm.DashHeld)
        {
            sm.SwitchState(sm.swimState);
            return;
        }
    }

    public override void FixedUpdateState()
    {
        // 持续推力，和原版逻辑一致（漂移生效时用漂移方向）
        if (sm.HasMoveInput)
        {
            Vector2 eff = sm.GetEffectiveMoveDir();
            Vector2 moveDir = new Vector2(
                sm.flipMove ? -eff.x : eff.x,
                eff.y
            );
            if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();

            float effForce = sm.swimForce * sm.sprintForceMultiplier;
            sm.rb.AddForce(new Vector3(moveDir.x, moveDir.y, 0f) * effForce, ForceMode.Force);
        }

        // 冲刺限速
        float dashMaxSpeed = sm.maxSpeed * sm.sprintSpeedMultiplier;
        sm.ClampPlaneVelocity(dashMaxSpeed);

        // 转向逻辑，和游泳同源（按住右键跟鼠标，否则跟移动方向）
        if (sm.TryGetFacingDir(out Vector2 faceDir))
        {
            sm.ApplyFacing(faceDir);
        }
    }

    public override void Exit()
    {
    }
}
