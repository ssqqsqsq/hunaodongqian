using UnityEngine;

// Debuff 表的单位约定：表里写什么，游戏里就是什么，读取时不做任何换算
// 唯一的特殊值：时长为 -1 表示无限
public static class DebuffScale
{
    // 表时长 → 秒；-1 代表无限时长
    public static float TimeFromTable(int raw)
    {
        return raw < 0 ? -1f : raw;
    }

    // 秒 → 表内整数（导表/工具反向转换）
    public static int TimeToTable(float seconds)
    {
        return Mathf.RoundToInt(seconds);
    }
}

// 属性值域表，和角色组件上限保持同步
public static class AttributeDomain
{
    public static bool TryGet(DataType attribute, out float min, out float max)
    {
        switch (attribute)
        {
            case DataType.CarbonDioxide: min = 0f; max = 300f; return true;
            case DataType.Nitrogen: min = 0f; max = 30f; return true;
            case DataType.HeartRate: min = 0f; max = 250f; return true;
            case DataType.Depth: min = 0f; max = 100f; return true;
            case DataType.Oxygen: min = 0f; max = 3600f; return true;
            case DataType.IsDead: min = 0f; max = 1f; return true;
            case DataType.OxygenRate: min = 0f; max = 200f; return true;
            case DataType.CarbonDioxideRate: min = -1000f; max = 1000f; return true;
            case DataType.NitrogenRate: min = -100f; max = 100f; return true;
            case DataType.CurrentOxygen: min = 0f; max = 1800f; return true;
            default: min = 0f; max = 0f; return false;
        }
    }

    public static bool IsDefined(DataType attribute)
    {
        return TryGet(attribute, out _, out _);
    }
}
