using UnityEngine;

// Debuff表单位换算入口
// 规则：大部分属性表内值 = 物理值 * 10；Depth深度直接存米，不缩放
// 业务拿到的DebuffConfig已是物理单位，禁止自行 /10
// 修改Factor时，JSON表同步更新
public static class DebuffScale
{
    // 缩放倍数
    public const int Factor = 10;

    // 该属性是否需要缩放，Depth除外
    public static bool IsScaled(DataType attribute)
    {
        return attribute != DataType.Depth;
    }

    // 表原始整数 → 物理值
    public static float ValueFromTable(DataType attribute, int raw)
    {
        return IsScaled(attribute) ? raw / (float)Factor : raw;
    }

    // 表时长(0.1s单位) → 秒；-1代表无限时长
    public static float TimeFromTable(int raw)
    {
        return raw < 0 ? -1f : raw / (float)Factor;
    }

    // Effect操作数原始值 → 物理值
    public static float OperandFromTable(int raw)
    {
        return raw / (float)Factor;
    }

    // 物理值 → 表内整数（导表/工具反向转换）
    public static int ToTable(DataType attribute, float value)
    {
        return Mathf.RoundToInt(IsScaled(attribute) ? value * Factor : value);
    }

    // 秒 → 表内0.1s整数（导表/工具反向转换）
    public static int TimeToTable(float seconds)
    {
        return Mathf.RoundToInt(seconds * Factor);
    }
}

// 属性值域表，和角色组件上限保持同步
public static class AttributeDomain
{
    public static bool TryGet(DataType attribute, out float min, out float max)
    {
        switch (attribute)
        {
            case DataType.CarbonDioxide: min = 0f; max = 30f; return true;
            case DataType.Nitrogen: min = 0f; max = 30f; return true;
            case DataType.HeartRate: min = 0f; max = 250f; return true;
            case DataType.Depth: min = 0f; max = 100f; return true;
            case DataType.Oxygen: min = 0f; max = 360f; return true;
            case DataType.IsDead: min = 0f; max = 1f; return true;
            case DataType.OxygenRate: min = 0f; max = 20f; return true;
            case DataType.CarbonDioxideRate: min = -100f; max = 100f; return true;
            case DataType.NitrogenRate: min = -100f; max = 100f; return true;
            case DataType.CurrentOxygen: min = 0f; max = 180f; return true;
            default: min = 0f; max = 0f; return false;
        }
    }

    public static bool IsDefined(DataType attribute)
    {
        return TryGet(attribute, out _, out _);
    }
}
