using UnityEngine.TextCore.Text;

public class DeathState : BaseState
{
    private readonly PlayerSM sm;

    public DeathState(PlayerSM stateMachine) : base(stateMachine)
    {
        sm = stateMachine;
    }

    public override void Enter()
    {
        // 死亡进入后可以：播放死亡动画、停止物理、抛事件
        // sm.anim.SetTrigger("Death");
        // sm.rb.isKinematic = true;
    }

    public override void UpdateState()
    {
        // 死亡是终态，不再切换任何状态
    }

    public override void FixedUpdateState()
    {
        // 死亡后不再处理移动、转向
    }

    public override void Exit()
    {
        // 死亡状态一般不会退出，这里留空
    }
}
