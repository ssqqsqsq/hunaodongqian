
using UnityEngine;


public enum DataType
{
    CarbonDioxide = 1,  // 碳酸值
    Nitrogen = 2,       // 氮醉值
    HeartRate = 3,       // 心率
    Depth = 4,           // 深度
    Oxygen = 5,         // 总氧气值
    IsDead = 6,          // 是否死亡
    OxygenRate = 7,       // 氧气消耗率
    CarbonDioxideRate = 8, // 碳酸增长率
    NitrogenRate = 9,      // 氮醉增长率
    CurrentOxygen = 10,     //现在气瓶氧气值
}




public class DataManager : MonoBehaviour
{
    [Header("属性组件引用")]
    [SerializeField] private OxygenComponent oxygenComponent;
    [SerializeField] private NitrogenNarcosisComponent nitrogenComponent;
    [SerializeField] private CarbonDioxideComponent carbonDioxideComponent;
    [SerializeField] private DepthComponent depthComponent;
    [SerializeField] private PlayerSM playerSM;
    [SerializeField] private HeartRateComponent heartRateComponent;
    // 获取指定属性的当前数值
    public float GetValue(DataType type)
    {
        switch (type)
        {
            // 碳酸值
            case DataType.CarbonDioxide:
                return carbonDioxideComponent.CurrentCarbonDioxide;

            // 氮醉值
            case DataType.Nitrogen:
                return nitrogenComponent.CurrentNitrogenNarcosis;

            // 心率（待实现）
            case DataType.HeartRate:
                return heartRateComponent.CurrentHeartRate;

            // 深度
            case DataType.Depth:
                return depthComponent.CurrentDepth;

            // 总氧气值
            case DataType.Oxygen:
                return oxygenComponent.TotalOxygen;

            // 是否死亡
            case DataType.IsDead:
                return playerSM.IsDead ? 1f : 0f;

            // 氧气消耗率
            case DataType.OxygenRate:
                return oxygenComponent.CurrentOxygenDepletionRate;

            // 碳酸增长率
            case DataType.CarbonDioxideRate:
                return carbonDioxideComponent.CurrentChangeRate;

            // 氮醉增长率
            case DataType.NitrogenRate:
                return nitrogenComponent.CurrentChangeRate;

            // 当前气瓶氧气值
            case DataType.CurrentOxygen:
                return oxygenComponent.CurrentOxygen;

            default:
                Debug.LogWarning($"未找到属性：{type}");
                return 0f;
        }
    }

}
