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
        // 优先级：死亡 > 移动(冲刺/游泳) > 待机
        if (sm.IsDead)
        {
            sm.SwitchState(sm.deathState);
            return;
        }

        if (sm.HasMoveInput)
        {
            idleTimer = 0;
            // 移动中按住 Shift 才进入冲刺
            if (sm.DashHeld)
            {
                sm.SwitchState(sm.dashState);
                return;
            }
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

        // 施加游动力（漂移生效时用漂移方向）
        Vector2 eff = sm.GetEffectiveMoveDir();
        if (eff.sqrMagnitude > 0.01f)
        {
            Vector2 moveDir = new Vector2(
                sm.flipMove ? -eff.x : eff.x,
                eff.y
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
