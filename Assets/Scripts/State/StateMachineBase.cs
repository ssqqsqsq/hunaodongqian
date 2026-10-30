using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class StateMachineBase : MonoBehaviour
{
    protected BaseState currentState;

    protected virtual void Start()
    {
        // 子类自己设置初始状态
    }

    protected virtual void Update()
    {
        currentState?.UpdateState();
    }

    protected virtual void FixedUpdate()
    {
        currentState?.FixedUpdateState();
    }

  
      //切换状态
  
    public virtual void ChangeState(BaseState newState)
    {
        currentState?.Exit();
        currentState = newState;
        currentState.Enter();
    }
}
