using UnityEngine;

public class GasManager : MonoBehaviour
{
    public PlayerStateMachine sm;

    [Header("气罐设置")]
    public float MaxPerTank = 180f;
    public int TankCount = 2;

    public float Current { get; private set; }
    public float Max => MaxPerTank * TankCount;

    // 预留给后续 debuff 使用
    public float RateModifier { get; set; } = 0f;

    void Awake()
    {
        Current = Max; // 开局满气 360
    }

    void Update()
    {
        if (sm == null) return;

        // 基础速率 + debuff速率
        float rate = sm.CurrentData.GasRate + RateModifier;

        // 扣减气体
        Current = Mathf.Max(0f, Current - rate * Time.deltaTime);

        // 气体耗尽则死亡
        if (Current <= 0f && !sm.IsDead)
        {
            sm.MarkDead();
        }
    }
}