using UnityEngine;
using UnityEngine.UI;

public class DashboardDataUI : MonoBehaviour
{
    [Header("UI 文本引用")]
    [Tooltip("显示心率的 Text 组件")]
    public Text heartRateText;
    [Tooltip("显示深度的 Text 组件")]
    public Text depthText;

    [Header("数值前缀/后缀（可选）")]
    public string heartRateSuffix = " bpm";
    public string depthSuffix = " m";

    [Header("数据组件引用（留空会自动找）")]
    public HeartRateComponent heartRateComp;
    public DepthComponent depthComp;
    public EffectController effectCtrl;

    // 记录乱跳的假数据
    private float fakeHeartRate;
    private float fakeDepth;
    private float glitchTimer;

    void Start()
    {
        // 自动寻找组件
        if (heartRateComp == null) heartRateComp = FindFirstObjectByType<HeartRateComponent>();
        if (depthComp == null) depthComp = FindFirstObjectByType<DepthComponent>();
        if (effectCtrl == null) effectCtrl = FindFirstObjectByType<EffectController>();

        if (heartRateComp == null) Debug.LogError("DashboardDataUI: 找不到 HeartRateComponent");
        if (depthComp == null) Debug.LogError("DashboardDataUI: 找不到 DepthComponent");
        if (effectCtrl == null) Debug.LogError("DashboardDataUI: 找不到 EffectController");
    }

    void Update()
    {
        if (heartRateComp == null || depthComp == null) return;

        // 1. 获取真实数值
        float realHeartRate = heartRateComp.CurrentHeartRate;
        float realDepth = depthComp.CurrentDepth; // 如果你想要实时深度，可以换成 GetRealTimeDepth()

        // 2. 判断是否处于“数值显示错误”的 Debuff 状态
        bool isGlitching = effectCtrl != null && effectCtrl.IsValueGlitchActive;

        float displayHeartRate = realHeartRate;
        float displayDepth = realDepth;

        if (isGlitching)
        {
            // --- 效果5：数值显示错误逻辑 ---
            // 每 0.1 秒随机跳变一次，数字在真实值上下浮动 ±20 左右
            glitchTimer -= Time.deltaTime;
            if (glitchTimer <= 0f)
            {
                glitchTimer = 0.1f; // 刷新频率
                fakeHeartRate = realHeartRate + Random.Range(-20f, 20f);
                fakeDepth = realDepth + Random.Range(-15f, 15f);
            }
            displayHeartRate = fakeHeartRate;
            displayDepth = fakeDepth;
        }

        // 3. 更新 UI 文本显示
        // 心率取整数
        if (heartRateText != null)
            heartRateText.text = Mathf.Max(0, Mathf.RoundToInt(displayHeartRate)).ToString() + heartRateSuffix;

        // 深度保留一位小数
        if (depthText != null)
            depthText.text = Mathf.Max(0, displayDepth).ToString("F1") + depthSuffix;
    }
}