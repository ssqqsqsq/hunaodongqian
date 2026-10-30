using UnityEngine;

public class NitrogenManager : MonoBehaviour
{
    public PlayerStateMachine sm;

    [Header("数值范围")]
    public float Min = 0f;
    public float Max = 30f;

    public float Current { get; private set; }

    // 基础速率你设定为 -1
    public float BaseRate = -1f;
    public float RateModifier { get; set; } = 0f;

    void Update()
    {
        if (sm == null) return;

        float rate = BaseRate + RateModifier;

        Current = Mathf.Clamp(Current + rate * Time.deltaTime, Min, Max);
    }
}