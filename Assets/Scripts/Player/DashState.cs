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
        // 进入时给一小段初始冲量，起步顺滑，和你原来的手感对齐
        Vector2 dashDir = sm.HasMoveInput
            ? sm.MoveDir
            : new Vector2(sm.rb.velocity.x, sm.rb.velocity.y).normalized;

        if (dashDir.sqrMagnitude < 0.01f)
            dashDir = new Vector2(sm.facingSign, 0f);

        sm.rb.AddForce(
            new Vector3(dashDir.x, dashDir.y, 0f) * sm.swimForce * sm.sprintForceMultiplier * 0.5f,
            ForceMode.Impulse
        );
    }

    public override void UpdateState()
    {
        // 死亡优先
        if (sm.IsDead)
        {
            sm.SwitchState(sm.deathState);
            return;
        }

        // 松开Shift就退出冲刺
        if (!sm.DashHeld)
        {
            sm.SwitchState(sm.HasMoveInput ? sm.swimState : sm.idleState);
            return;
        }
    }

    public override void FixedUpdateState()
    {
        // 持续推力，和原版逻辑一致
        if (sm.HasMoveInput)
        {
            Vector2 moveDir = new Vector2(
                sm.flipMove ? -sm.MoveDir.x : sm.MoveDir.x,
                sm.MoveDir.y
            );
            if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();

            float effForce = sm.swimForce * sm.sprintForceMultiplier;
            sm.rb.AddForce(new Vector3(moveDir.x, moveDir.y, 0f) * effForce, ForceMode.Force);
        }

        // 冲刺限速
        float dashMaxSpeed = sm.maxSpeed * sm.sprintSpeedMultiplier;
        sm.ClampPlaneVelocity(dashMaxSpeed);

        // 转向逻辑，和游泳完全一样，手感统一
        if (sm.HasMoveInput)
        {
            Vector2 faceDir = new Vector2(
                sm.flipAngle ? -sm.MoveDir.x : sm.MoveDir.x,
                sm.MoveDir.y
            );
            if (faceDir.sqrMagnitude > 1f) faceDir.Normalize();

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
    }

    public override void Exit()
    {
    }
}
