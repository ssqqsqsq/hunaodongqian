using UnityEngine;

// 世界里可拾取/可手持的物品（收集品或气瓶）。
// 拾取时隐藏本体；放下/投掷时在原位置重新显示。
public class HoldableItem : MonoBehaviour
{
    [Header("物品信息")]
    public ItemData data = new ItemData();

    [Header("交互")]
    [Tooltip("玩家进入这个半径后可按 F 拾取")]
    public float pickupRadius = 1.8f;

    [Header("调试")]
    public bool verboseLog = true;

    // 是否已被拾起（世界物体隐藏中）
    public bool IsPickedUp { get; private set; }

    // 被拾起：隐藏本体
    public void PickUp()
    {
        IsPickedUp = true;
        gameObject.SetActive(false);
        if (verboseLog) Debug.Log($"[手持] 拾起：{data.displayName}");
    }

    // 重新放回世界
    public void PlaceInWorld(Vector3 pos)
    {
        transform.position = pos;
        gameObject.SetActive(true);
        IsPickedUp = false;
        if (verboseLog) Debug.Log($"[手持] 放下：{data.displayName} @ {pos}");
    }

    private void OnEnable()
    {
        // 重新激活时视为回到世界
        IsPickedUp = false;
    }
}
