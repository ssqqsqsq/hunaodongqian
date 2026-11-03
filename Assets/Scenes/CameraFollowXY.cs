using UnityEngine;

public class CameraFollowXY : MonoBehaviour
{
    [Header("跟随目标")]
    [Tooltip("要跟随的角色Transform")]
    public Transform target;

    [Header("跟随设置")]
    [Tooltip("跟随平滑度，0表示瞬间跟随，值越大越平滑")]
    [Range(0f, 20f)]
    public float smoothSpeed = 5f;

    [Tooltip("相机相对于角色的偏移量（XY平面上的偏移，Z轴的偏移决定相机距离）")]
    public Vector3 offset = new Vector3(0f, 0f, -10f);

    [Tooltip("是否锁定Z轴（相机Z坐标保持不变）")]
    public bool lockZ = true;

    [Header("边界限制（可选）")]
    public bool useBounds = false;
    public Vector2 minBounds = new Vector2(-20f, -20f);
    public Vector2 maxBounds = new Vector2(20f, 20f);

    private float fixedZ;

    void Start()
    {
        if (target == null)
        {
            Debug.LogError("[CameraFollowXY] 未指定跟随目标！");
            enabled = false;
            return;
        }

        // 记录初始Z轴位置，用于锁定
        fixedZ = transform.position.z;
    }

    void LateUpdate()
    {
        if (target == null) return;

        // 计算目标位置：只跟随XY，Z轴保持不变
        Vector3 desiredPosition = new Vector3(
            target.position.x + offset.x,
            target.position.y + offset.y,
            lockZ ? fixedZ : target.position.z + offset.z
        );

        // 应用边界限制
        if (useBounds)
        {
            desiredPosition.x = Mathf.Clamp(desiredPosition.x, minBounds.x, maxBounds.x);
            desiredPosition.y = Mathf.Clamp(desiredPosition.y, minBounds.y, maxBounds.y);
        }

        // 平滑插值移动
        Vector3 smoothedPosition = Vector3.Lerp(
            transform.position,
            desiredPosition,
            smoothSpeed * Time.deltaTime
        );

        transform.position = smoothedPosition;
    }
}