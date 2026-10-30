using UnityEngine;
using System;

public class DepthComponent : MonoBehaviour
{
    [Header("深度配置")]
    [SerializeField] private float surfaceY = 10f;
    [SerializeField] private float depthChangeThreshold = 0.5f;

    // 当前深度
    public float CurrentDepth { get; private set; }

    // 深度变化事件，传递当前深度
    public event Action<float> OnDepthChanged;

    private void Update()
    {
        UpdateDepth();
    }

    private void UpdateDepth()
    {
        // 计算当前深度
        float newDepth = Mathf.Max(0f, surfaceY - transform.position.y);

        // 判断深度变化是否达到阈值
        if (Mathf.Abs(newDepth - CurrentDepth) >= depthChangeThreshold)
        {
            CurrentDepth = newDepth;

            // 通知订阅者
            OnDepthChanged?.Invoke(CurrentDepth);
        }
    }
}