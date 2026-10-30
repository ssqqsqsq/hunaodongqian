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
        // 转向逻辑
        if (sm.HasMoveInput)
        {
            Vector2 faceDir = new Vector2(
                sm.flipAngle ? -sm.MoveDir.x : sm.MoveDir.x,
                sm.MoveDir.y
            );
            if (faceDir.sqrMagnitude > 1f) faceDir.Normalize();

            // 更新朝向翻转
            Vector2 rel = new Vector2(faceDir.x * sm.facingSign, faceDir.y);
            if (rel.x < -0.2f)
                sm.facingSign = -sm.facingSign;

            rel = new Vector2(faceDir.x * sm.facingSign, faceDir.y);
            float tilt = Mathf.Atan2(rel.y, rel.x) * Mathf.Rad2Deg;
            tilt = Mathf.Clamp(tilt, -sm.maxTilt, sm.maxTilt);

            float yaw = sm.facingSign > 0f ? 0f : 180f;
            float z = sm.rotationOffset - tilt;
            if (sm.invertZ) z = -z;

            Quaternion targetRot = Quaternion.Euler(0f, yaw, z);
            sm.rb.rotation = Quaternion.RotateTowards(
                sm.rb.rotation, targetRot,
                sm.rotateSpeed * Time.fixedDeltaTime
            );
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
