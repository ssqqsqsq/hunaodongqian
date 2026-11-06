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

// trigger 的操作类型，对应 JSON 里 trigger 的第一个整数
public enum TriggerKind
{
    Numeric = 0, // 数值比对：[0, Tag, 阈值, 运算符]
    Flag = 1,    // flag为真：[1, flagNum]
}

//effect 的操作类型（对应策划《效果/effect 细则》）
public enum EffectTag
{
    Unknown = 0,
    ScreenDarken = 1,       // 屏幕昏暗：边框向中间变暗
    ScreenDarker = 2,       // 屏幕更加昏暗
    ScreenDistort = 3,      // 屏幕扭曲（头晕/晕船感）
    ValueChange = 4,        // 产生数值变化：[4, 属性, 增量]，加减看符号
    ValueDisplayGlitch = 5, // 数值显示错误：气体/心率在 (0,20) 内乱跳
    FrameFreeze = 6,        // 画面暂留：每段时间留 0.5s，透明度 0→100 再恢复
    InputDelay = 7,         // 操作延迟：按键后 0.5s 才响应
    InputInvert = 8,        // 操作反向：上下左右颠倒，鼠标 180°
    RandomDrift = 9,        // 随机偏移：每 3~6s 让朝向随机偏 45°，优先级最高
    ForceSprint = 10,       // 强制疾跑：等同一直按住 Shift
    ScreenFlicker = 11,     // 视野闪烁（黑闪）
    Death = 12,             // 死亡（可做多种死法 + 亡语）
    QteMinigame = 13,       // QTE 小游戏
}

//触发条件trigger
[Serializable]
public class TriggerCondition
{
    //触发方式，对应 JSON 里 trigger 的第一个整数
    public TriggerKind kind = TriggerKind.Numeric;

    // ---- kind == Numeric ----
    //检测属性
    public DataType attribute;

    //阈值，物理单位
    public float threshold;

    //比较方式
    public TriggerOperator op;

    // ---- kind == Flag ----
    //flag表里的序号（1..5），该位为真时条件成立
    public int flagNum;

    //数值型用当前属性值判定；flag型忽略 value，直接查 flag 表
    public bool IsSatisfied(float value, FlagTable flags)
    {
        if (kind == TriggerKind.Flag)
        {
            return flags != null && flags.IsTrue(flagNum);
        }

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

    //数值型的便捷重载（flag型调用会返回 false）
    public bool IsSatisfied(float value)
    {
        return IsSatisfied(value, null);
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

    //同组随机：group 相同且触发条件相同的 Debuff，同时只允许一条生效（0 = 不参与随机）
    public int group;

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
