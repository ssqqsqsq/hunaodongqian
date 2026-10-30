using UnityEngine;

public class CO2Manager : MonoBehaviour
{
    public PlayerStateMachine sm;

    [Header("数值范围")]
    public float Min = 0f;
    public float Max = 30f;

    public float Current { get; private set; }
    public float RateModifier { get; set; } = 0f;

    void Update()
    {
        if (sm == null) return;

        float rate = sm.CurrentData.CO2Rate + RateModifier;

        // 限制在 0 ~ 30 之间
        Current = Mathf.Clamp(Current + rate * Time.deltaTime, Min, Max);
    }
}