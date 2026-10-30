using UnityEngine;

public class DepthManager : MonoBehaviour
{
    [Header("数值限制")]
    public float Min = -5f;
    public float Max = 400f;

    // 记录出生时的 Y 坐标
    private float startY;
    public float Depth { get; private set; }

    void Start()
    {
        // 游戏开始时，记录 body 的初始 Y 坐标
        startY = transform.position.y;
        Depth = 0f;
    }

    void Update()
    {
        // 核心逻辑：当前 Y 减去初始 Y。
        // 如果 Y 减小（往下潜），差值是负数，所以前面加个负号，让深度变成正数（往下潜深度增加）。
        // 如果 Y 增加（往上游），差值是正数，加负号后深度变成负数（往上浮深度减少）。
        float calculatedDepth = -(transform.position.y - startY);

        // 限制在 -5 到 400 之间
        Depth = Mathf.Clamp(calculatedDepth, Min, Max);
    }
}