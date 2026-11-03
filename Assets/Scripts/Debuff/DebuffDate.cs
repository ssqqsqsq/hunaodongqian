using System;
using System.Collections.Generic;
using UnityEngine;

// Debuff 的运行时数据结构，全部使用物理单位

public enum TriggerOperator
{
    Greater = 1,        // 大于
    Less = 2,           // 小于
    Equal = 3,          // 等于
    GreaterOrEqual = 4, // 大于等于
    LessOrEqual = 5,    // 小于等于
    NotEqual = 6,       // 不等于
}

//effect 的操作类型
public enum EffectTag
{
    Unknown = 0,
    ScreenDarken = 1,       // 屏幕昏暗
    ScreenDarker = 2,       // 屏幕更加昏暗
    ScreenDistort = 3,      // 屏幕扭曲
    ValueChange = 4,        // 产生数值变化
    ValueDisplayGlitch = 5, // 数值显示错误
    FrameFreeze = 6,        // 画面暂留
    InputDelay = 7,         // 操作延迟
    InputInvert = 8,        // 操作反向
    RandomDrift = 9,        // 随机偏移
    ForceSprint = 10,       // 强制疾跑
    ScreenFlicker = 11,     // 视野闪烁
    Death = 12,             // 死亡
    QteMinigame = 13,       // QTE 小游戏
}

//触发条件trigger
[Serializable]
public class TriggerCondition
{
    //检测属性
    public DataType attribute;

    //阈值，物理单位
    public float threshold;

    //比较方式
    public TriggerOperator op;

    public bool IsSatisfied(float value)
    {
        switch (op)
        {
            case TriggerOperator.Greater: return value > threshold;
            case TriggerOperator.Less: return value < threshold;
            case TriggerOperator.Equal: return Mathf.Approximately(value, threshold);
            case TriggerOperator.GreaterOrEqual: return value >= threshold;
            case TriggerOperator.LessOrEqual: return value <= threshold;
            case TriggerOperator.NotEqual: return !Mathf.Approximately(value, threshold);
            default: return false;
        }
    }
}

//一个效果单元
[Serializable]
public class EffectSpec
{
    //操作类型
    public EffectTag tag;

    //只有 EffectTag.ValueChange 用：要改哪个属性。
    public DataType attribute;

    //操作数，物理单位。
    //属性序号专门放在 attribute 字段里，不进这个数组，
    //这样 operands 里永远只有"值"，不会混进"标签"造成单位误读。
    public float[] operands = Array.Empty<float>();
}

//一条 Debuff 的运行时配置
[Serializable]
public class DebuffConfig
{
    public int id;

    //策划案名称
    public string name;

    //游戏内显示名称
    public string nameForGame;

    public TriggerCondition trigger;

    //观察时长（秒），-1 表示无限。
    public float observeTime;

    //宽容时长（秒），-1 表示无限。
    public float graceTime;

    //疗愈时长（秒），-1 表示无限。
    public float healTime;

    public List<EffectSpec> effects = new List<EffectSpec>();

    public bool ObserveInfinite { get { return observeTime < 0f; } }
    public bool GraceInfinite { get { return graceTime < 0f; } }
    public bool HealInfinite { get { return healTime < 0f; } }
}
