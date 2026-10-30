using UnityEngine;

// 所有状态的基类
public abstract class BaseState
{
    protected StateMachineBase stateMachine;

    protected BaseState(StateMachineBase sm)
    {
        stateMachine = sm;
    }

    // 进入这个状态时执行一次
    public abstract void Enter();
    // 每帧执行
    public abstract void UpdateState();
    // FixedUpdate
    public abstract void FixedUpdateState();
    // 退出状态时执行一次
    public abstract void Exit();
}