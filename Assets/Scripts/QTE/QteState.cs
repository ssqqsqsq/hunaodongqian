using UnityEngine;

// 全局 QTE 忙状态：任一 QTE 进行中时，功能按钮不可点击。
public static class QteState
{
    private static int busy;

    public static bool Busy => busy > 0;

    public static void Begin() => busy++;

    public static void End() => busy = Mathf.Max(0, busy - 1);
}
