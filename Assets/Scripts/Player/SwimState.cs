using UnityEngine;
using UnityEngine.TextCore.Text;

public class SwimState : BaseState
{
    private readonly PlayerSM sm;
    private float idleTimer;
    private const float idleWait = 0.2f; // 松键后延迟切待机，防手感突兀

    public SwimState(PlayerSM stateMachine) : base(stateMachine)
    {
        sm = stateMachine;
    }

    public override void Enter()
    {
        idleTimer = 0;
        // 进入游泳状态可以加动画触发，比如 sm.anim.SetBool("Swim", true);
    }

    public override void UpdateState()
    {
        // 优先级：死亡 > 冲刺 > 移动/待机
        if (sm.IsDead)
        {
            sm.SwitchState(sm.deathState);
            return;
        }

        if (sm.DashHeld)
        {
            sm.SwitchState(sm.dashState);
            return;
        }

        // 无输入计时，超时切待机
        if (sm.HasMoveInput)
        {
            idleTimer = 0;
        }
        else
        {
            idleTimer += Time.deltaTime;
            if (idleTimer >= idleWait)
            {
                sm.SwitchState(sm.idleState);
            }
        }
    }

    public override void FixedUpdateState()
    {
        // 转向：按住右键跟鼠标，否则跟移动方向
        if (sm.TryGetFacingDir(out Vector2 faceDir))
        {
            sm.ApplyFacing(faceDir);
        }

        // 施加游动力
        if (sm.MoveDir.sqrMagnitude > 0.01f)
        {
            Vector2 moveDir = new Vector2(
                sm.flipMove ? -sm.MoveDir.x : sm.MoveDir.x,
                sm.MoveDir.y
            );
            if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();

            sm.rb.AddForce(new Vector3(moveDir.x, moveDir.y, 0f) * sm.swimForce, ForceMode.Force);
            sm.ClampPlaneVelocity(sm.maxSpeed);
        }
    }

    public override void Exit()
    {
        // 1
    }
}
