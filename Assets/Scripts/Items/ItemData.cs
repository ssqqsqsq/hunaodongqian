using UnityEngine;

// 手持物类型
public enum ItemKind
{
    Collectible = 1, // 收集品
    AirTank = 2,     // 气瓶
}

// 一个物品的运行时数据（世界物品 / 手持内容都用它）
[System.Serializable]
public class ItemData
{
    public ItemKind kind = ItemKind.Collectible;
    public string displayName = "物品";
    public Sprite icon;
    [TextArea] public string description;

    [Header("气瓶专用")]
    public float gasRemaining;
    public float gasMax;

    public ItemData Clone()
    {
        return new ItemData
        {
            kind = kind,
            displayName = displayName,
            icon = icon,
            description = description,
            gasRemaining = gasRemaining,
            gasMax = gasMax,
        };
    }
}
