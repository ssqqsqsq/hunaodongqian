using UnityEngine;

public class DepthIndicatorUI : MonoBehaviour
{
    [Header("UI 引用")]
    public GameObject downUI; // 向下箭头
    public GameObject upUI;   // 向上箭头

    [Header("目标玩家")]
    [Tooltip("留空会自动寻找带有 PlayerSM 的玩家")]
    public Transform playerTransform;

    [Header("灵敏度")]
    [Tooltip("Y 轴变化超过这个值才算移动（太小会闪烁，太大反应慢）")]
    public float changeThreshold = 0.02f;

    [Tooltip("勾选：停在水里不动时隐藏箭头。不勾选：保持最后的箭头")]
    public bool hideWhenIdle = true;

    private float lastY;

    void Start()
    {
        // 自动找玩家
        if (playerTransform == null)
        {
            var player = FindFirstObjectByType<PlayerSM>();
            if (player != null) playerTransform = player.transform;
        }

        // 开局记录一下当前的 Y 坐标
        if (playerTransform != null)
        {
            lastY = playerTransform.position.y;
        }

        // 开局默认把两个图标全关掉
        if (downUI != null) downUI.SetActive(false);
        if (upUI != null) upUI.SetActive(false);
    }

    void Update()
    {
        if (playerTransform == null) return;

        // 1. 获取当前的 Y 坐标
        float currentY = playerTransform.position.y;

        // 2. 算差值：如果是正数，说明 Y 变大了（在往上走）；负数说明 Y 变小了（在往下走）
        float yDifference = currentY - lastY;

        // 3. 判断状态
        if (yDifference > changeThreshold)
        {
            // 坐标变大 = 往上走 = 显示 UP
            if (upUI != null && !upUI.activeSelf) upUI.SetActive(true);
            if (downUI != null && downUI.activeSelf) downUI.SetActive(false);
        }
        else if (yDifference < -changeThreshold)
        {
            // 坐标变小 = 往下走 = 显示 DOWN
            if (downUI != null && !downUI.activeSelf) downUI.SetActive(true);
            if (upUI != null && upUI.activeSelf) upUI.SetActive(false);
        }
        else if (hideWhenIdle)
        {
            // 没怎么动，如果勾选了静止隐藏，就都关掉
            if (upUI != null && upUI.activeSelf) upUI.SetActive(false);
            if (downUI != null && downUI.activeSelf) downUI.SetActive(false);
        }

        // 4. 把当前的 Y 存起来，下一帧对比用
        lastY = currentY;
    }
}