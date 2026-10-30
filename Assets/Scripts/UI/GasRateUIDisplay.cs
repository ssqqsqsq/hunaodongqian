using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class GasRateUIDisplay : MonoBehaviour
{
    public PlayerStateMachine sm;
    public TMP_Text targetText; // 拖入你的文本组件

    [Header("延迟设置")]
    public float Delay = 1.5f;

    // 用于记录历史数据
    private struct RateSample
    {
        public float time;
        public float rate;
    }
    private List<RateSample> history = new List<RateSample>();

    void Update()
    {
        if (sm == null || targetText == null) return;

        // 1. 获取当前真实的基础气体消耗速率（含 Debuff 修正）
        // 这里你可以用 sm.CurrentData.GasRate，如果你想精确计算 debuff，可以加上 DebuffManager 修正值
        float currentRate = sm.CurrentData.GasRate;

        // 2. 将当前时间和速率记录到历史列表中
        history.Add(new RateSample { time = Time.time, rate = currentRate });

        // 3. 清理过期的数据（保留 1.5 秒前的那一个记录）
        float targetTime = Time.time - Delay;

        // 只要历史列表里第二个数据的时间依然小于目标时间，说明第一个数据已经没用了，删掉它
        while (history.Count > 1 && history[1].time <= targetTime)
        {
            history.RemoveAt(0);
        }

        // 4. 取出队列最前面的值（即 1.5 秒前的值）
        float displayRate = history[0].rate;

        // 5. 将速率转换为文字并显示
        targetText.text = GetBreathText(displayRate);
    }

    // 映射文案（左开右闭）
    private string GetBreathText(float rate)
    {
        // 因为浮点数有精度问题，如果绝对值极小，就当0处理
        if (Mathf.Abs(rate) < 0.01f) return "呼吸停止";
        if (rate > 0f && rate <= 1.0f) return "微弱呼吸";
        if (rate > 1.0f && rate <= 1.5f) return "缓慢呼吸";
        if (rate > 1.5f && rate <= 3.0f) return "匀速呼吸";
        if (rate > 3.0f && rate <= 5.5f) return "急促呼吸";
        if (rate > 5.5f) return "呼吸紊乱";

        return "未知状态";
    }
}