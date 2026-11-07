using System;
using UnityEngine;

// 可交互收集品：玩家进入拾取半径后按 F 拾取。
// 拾取后登记到 LevelStats，供结算界面展示。
public class Collectible : MonoBehaviour
{
    [Header("收集品信息")]
    [SerializeField] private string itemId = "collectible";
    [SerializeField] private string displayName = "收集品";
    [SerializeField] private Sprite icon;

    [Header("交互设置")]
    [Tooltip("玩家进入这个半径后按 F 拾取")]
    [SerializeField] private float pickupRadius = 1.5f;
    [Tooltip("必须按 F 才能拾取；关掉则靠近自动拾取")]
    [SerializeField] private bool requireInteract = true;
    [Tooltip("同一帧按下 F 只收最靠近的一个收集品，避免一次按键收走多个")]
    [SerializeField] private bool onlyNearestPerFrame = true;

    [Header("调试")]
    [Tooltip("打印拾取相关日志")]
    [SerializeField] private bool verboseLog = true;

    // 拾取时广播
    public event Action<CollectibleData> OnCollected;

    // 同一帧只允许一个收集品响应 F
    private static int lastInteractFrame = -1;
    private static Collectible lastInteracted;

    private bool collected;
    private bool wasInRange;
    private PlayerSM player;
    private LevelStats stats;

    private void Update()
    {
        if (collected) return;

        if (player == null)
        {
            player = FindFirstObjectByType<PlayerSM>();
            if (player == null) return;
            if (verboseLog) Debug.Log($"[结算][收集品] {name} 找到玩家");
        }
        if (player.IsDead) return;

        Vector2 delta = (Vector2)(player.transform.position - transform.position);
        bool inRange = delta.sqrMagnitude <= pickupRadius * pickupRadius;

        if (inRange != wasInRange)
        {
            wasInRange = inRange;
            if (verboseLog)
                Debug.Log($"[结算][收集品] {name} {(inRange ? "进入" : "离开")}拾取范围（半径 {pickupRadius}）");
        }

        if (!inRange) return;

        if (requireInteract)
        {
            if (!Input.GetKeyDown(KeyCode.F)) return;
            if (verboseLog) Debug.Log($"[结算][收集品] {name} 检测到 F 键");

            if (onlyNearestPerFrame)
            {
                if (lastInteractFrame == Time.frameCount && lastInteracted != this)
                    return;
                lastInteractFrame = Time.frameCount;
                lastInteracted = this;
            }
        }

        Collect();
    }

    private void Collect()
    {
        collected = true;

        var data = new CollectibleData(itemId, displayName, icon);

        if (stats == null) stats = FindFirstObjectByType<LevelStats>();
        if (stats != null) stats.AddCollectible(data);
        else Debug.LogWarning($"[结算][收集品] {name} 找不到 LevelStats，拾取只隐藏、不登记");

        OnCollected?.Invoke(data);

        if (verboseLog) Debug.Log($"[结算][收集品] {name} 已拾取：{displayName} ({itemId})");

        // 隐藏本体；不销毁，避免事件订阅方引用丢失
        gameObject.SetActive(false);
    }
}
