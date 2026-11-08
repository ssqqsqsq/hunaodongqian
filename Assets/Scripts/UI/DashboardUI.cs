using UnityEngine;
using UnityEngine.UI;

public class DashboardUI : MonoBehaviour
{
    [Header("UI 引用")]
    public RectTransform pointerTransform;
    public Image fanShapeImage; // 新增：拖入扇形图片

    [Header("数据引用")]
    public OxygenComponent oxygen;

    [Header("旋转映射")]
    public float maxGasAngle = 90f;  // 满气角度（正上方）
    public float minGasAngle = -90f; // 空瓶角度（正下方）
    public float smoothSpeed = 5f;

    void Start()
    {
        if (oxygen == null) oxygen = FindFirstObjectByType<OxygenComponent>();
    }

    void Update()
    {
        if (oxygen == null || pointerTransform == null) return;

        // 1. 计算剩余百分比
        float currentGas = oxygen.CurrentOxygen;
        float maxGas = oxygen.MaxOxygen;
        float percent = maxGas > 0 ? (currentGas / maxGas) : 0f;
        percent = Mathf.Clamp01(percent);

        // 2. 指针的旋转逻辑（保持不变）
        float targetAngle = Mathf.Lerp(minGasAngle, maxGasAngle, percent);
        Quaternion targetRotation = Quaternion.Euler(0f, 0f, targetAngle);
        pointerTransform.localRotation = Quaternion.Lerp(pointerTransform.localRotation, targetRotation, Time.deltaTime * smoothSpeed);

        // 3. 扇形（折扇）的逻辑
        if (fanShapeImage != null)
        {
            // 扇形的 Fill Amount 范围是 0 到 1。
            // 我们的表盘从 -90度 到 90度，总共180度，占整个圆的 180/360 = 0.5。
            // 所以满气时，Fill Amount 应该是 0.5。
            float targetFill = percent * 0.5f;

            // 让扇形填充量平滑变化
            fanShapeImage.fillAmount = Mathf.Lerp(fanShapeImage.fillAmount, targetFill, Time.deltaTime * smoothSpeed);

            // 扇形的旋转：因为 Fill Origin 设置的是 Right (3点钟方向)，而表盘是从 -90度 (6点钟方向) 开始的
            // 所以我们需要把扇形逆时针转 90度（也就是让它指向正下方），它才会从下面开始展开。
            // 加上 -90f 的偏移，让扇形的起点从右边对齐到正下方
            fanShapeImage.rectTransform.localRotation = Quaternion.Euler(0f, 0f, minGasAngle - 270f);
        }
    }
}