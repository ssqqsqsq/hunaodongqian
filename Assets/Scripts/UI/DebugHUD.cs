using UnityEngine;

public class DebugHUD : MonoBehaviour
{
    public GasManager gas;
    public CO2Manager co2;
    public NitrogenManager nitrogen;
    public StaminaManager stamina;
    public HeartRateManager heart; // 马上要建的
    public DepthManager depth;     // 马上要建的

    void OnGUI()
    {
        // 在屏幕左上角画一个大大的调试面板
        GUIStyle style = new GUIStyle();
        style.fontSize = 24;
        style.normal.textColor = Color.white;

        string text = "======= 实时数据 =======\n";
        if (gas != null) text += $"气体: {gas.Current:F1} / {gas.Max}\n";
        if (stamina != null) text += $"体力: {stamina.Current:F1} / {stamina.Max}\n";
        if (co2 != null) text += $"碳酸: {co2.Current:F1}\n";
        if (nitrogen != null) text += $"氮醉: {nitrogen.Current:F1}\n";

        // 占位，下面步骤加了脚本再连
        if (heart != null) text += $"心率: {heart.DisplayValue:F0}\n";
        if (depth != null) text += $"深度: {depth.Depth:F1} m\n";

        GUI.Label(new Rect(20, 20, 400, 300), text, style);
    }
}