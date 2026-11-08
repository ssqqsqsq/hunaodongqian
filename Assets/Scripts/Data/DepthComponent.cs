using UnityEngine;
using System;

public class DepthComponent : MonoBehaviour
{
    [Header("深度配置")]
    [SerializeField] private float depthChangeThreshold = 0.5f;
    [SerializeField] private GameObject spawnPoint;

    // 当前深度（受 0.5 米阈值限制）
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
        float newDepth = GetRealTimeDepth();

        // 判断深度变化是否达到阈值
        if (Mathf.Abs(newDepth - CurrentDepth) >= depthChangeThreshold)
        {
            CurrentDepth = newDepth;

            // 通知订阅者
            OnDepthChanged?.Invoke(CurrentDepth);
        }
    }

    /// <summary>
    /// 供外部 UI 直接获取实时深度（不受 0.5 米阈值限制）
    /// </summary>
    public float GetRealTimeDepth()
    {
        if (spawnPoint == null) return 0f; // 防止空引用报错
        return Mathf.Max(0f, spawnPoint.transform.position.y - transform.position.y);
    }
}