using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HUDController : MonoBehaviour
{
    [Header("数据源")]
    public GasManager gas;
    public StaminaManager stamina;
    public HeartRateManager heart;
    public DepthManager depth;

    [Header("UI 引用")]
    public Slider gasBar;
    public Slider staminaBar;

    [Header("文本引用 (可选)")]
    public TMP_Text heartText;
    public TMP_Text depthText;

    void Update()
    {
        // 更新气体条
        if (gas != null && gasBar != null)
        {
            gasBar.maxValue = gas.Max;
            gasBar.value = gas.Current;
        }

        // 更新体力条
        if (stamina != null && staminaBar != null)
        {
            staminaBar.maxValue = stamina.Max;
            staminaBar.value = stamina.Current;
        }

        // 更新心率文本
        if (heart != null && heartText != null)
        {
            heartText.text = $"心率: {Mathf.RoundToInt(heart.DisplayValue)}";
        }

        // 更新深度文本
        if (depth != null && depthText != null)
        {
            depthText.text = $"深度: {depth.Depth:F1} m";
        }
    }
}