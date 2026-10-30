using UnityEngine;

public class HeartRateManager : MonoBehaviour
{
    public float BaseRate = 90f;
    public float DebuffModifier = 0f;
    public float DisplayRefresh = 0.3f;

    public float DisplayValue { get; private set; }
    private float timer;

    void Start()
    {
        DisplayValue = BaseRate;
    }

    void Update()
    {
        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            timer = DisplayRefresh;
            // 实时心率 + 随机波动 (-5 到 5)
            float realRate = BaseRate + DebuffModifier;
            DisplayValue = realRate + Random.Range(-5f, 5f);
        }
    }
}