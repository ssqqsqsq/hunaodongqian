
using UnityEngine;


// 数值 id 与策划《数值id 细则》一一对应，改这里必须同步改表
// 1..8 是表内合法 id；100+ 仅程序内部使用，不写进 Debuff 表
public enum DataType
{
    CarbonDioxide = 1,      // 碳酸值
    Nitrogen = 2,           // 氮醉值
    HeartRate = 3,          // 心率
    Depth = 4,              // 深度
    Oxygen = 5,             // 总气体总量
    OxygenRate = 6,         // 气体消耗速率
    CarbonDioxideRate = 7,  // 碳酸值增长速率
    NitrogenRate = 8,       // 氮醉值增长速率

    // ---- 以下不在策划表内，仅内部使用 ----
    IsDead = 100,           // 是否死亡（0/1）
    CurrentOxygen = 101,    // 当前气瓶剩余氧气值
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
