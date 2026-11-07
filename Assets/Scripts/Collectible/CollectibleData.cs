using System;
using UnityEngine;

// 收集品数据：结算界面展示用。
// 图标可以为空（为空时结算界面显示色块 + 名字）。
[Serializable]
public class CollectibleData
{
    public string id;
    public string displayName;
    public Sprite icon;

    public CollectibleData() { }

    public CollectibleData(string id, string displayName, Sprite icon)
    {
        this.id = id;
        this.displayName = displayName;
        this.icon = icon;
    }
}
