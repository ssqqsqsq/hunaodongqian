
using System;
using UnityEngine;

public class NitrogenNarcosisComponent : MonoBehaviour
{
    [Header("氮醉值配置")]
    [SerializeField] private float maxNitrogenNarcosis = 30f;
    [SerializeField] private float minNitrogenNarcosis = 0f;
    [SerializeField] private float hyperThreshold = 20f;

    [Header("氮醉值变化速率")]
    [SerializeField] private float baseChangeRate = -1f;

    // 当前氮醉值
    public float CurrentNitrogenNarcosis { get; private set; }

    // 当前氮醉值变化速率
    public float CurrentChangeRate { get; private set; }

 

    // 氮醉值变化事件
    public event Action<float> OnNitrogenNarcosisChanged;

  

    private void Awake()
    {
        // 初始化氮醉值
        CurrentNitrogenNarcosis = minNitrogenNarcosis;

        // 初始化变化速率
        CurrentChangeRate = baseChangeRate;
    }

    private void Update()
    {
        UpdateNitrogenNarcosis();
    }

    // 更新氮醉值
    private void UpdateNitrogenNarcosis()
    {
        float oldValue = CurrentNitrogenNarcosis;

        // 根据变化速率计算氮醉值
        CurrentNitrogenNarcosis += CurrentChangeRate * Time.deltaTime;

        // 限制数值范围
        CurrentNitrogenNarcosis = Mathf.Clamp(
            CurrentNitrogenNarcosis,
            minNitrogenNarcosis,
            maxNitrogenNarcosis
        );

        // 数值发生变化时触发事件
        if (!Mathf.Approximately(oldValue, CurrentNitrogenNarcosis))
        {
            OnNitrogenNarcosisChanged?.Invoke(CurrentNitrogenNarcosis);
        }

     
    }


}
