using UnityEngine;
using UnityEngine.UI;

// QTE / HUD 共用的运行时 UI 工具（画布、文本、圆环、圆弧贴图等）。
public static class QteUi
{
    public static Font GetFont()
    {
        Font font = Font.CreateDynamicFontFromOSFont(
            new[] { "Microsoft YaHei", "Microsoft YaHei UI", "SimHei", "SimSun", "PingFang SC" }, 20);
        if (font != null) return font;

        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return font;
    }

    // 创建全屏覆盖画布
    public static Canvas CreateOverlay(Transform parent, string name, int sortingOrder)
    {
        var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler));
        go.transform.SetParent(parent, false);

        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    public static Image NewImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    public static Text NewText(Transform parent, string name, int fontSize, TextAnchor anchor, Color color)
    {
        var go = new GameObject(name, typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.font = GetFont();
        t.fontSize = fontSize;
        t.alignment = anchor;
        t.color = color;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    public static RectTransform NewRect(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    public static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    // 通用摆放：指定 anchor、pivot、位置、大小
    public static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
    }

    // 屏幕左下角：x/y 为到左下角的距离，pivot 在左下
    public static void BottomLeft(RectTransform rt, float x, float y, float w, float h)
    {
        Place(rt, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(x, y), new Vector2(w, h));
    }

    // 屏幕右下角：x 为到右下角的距离，y 为距底边高度，pivot 在右下
    public static void BottomRight(RectTransform rt, float xFromRight, float y, float w, float h)
    {
        Place(rt, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-xFromRight, y), new Vector2(w, h));
    }

    // 屏幕左侧竖直中心：x 到左边距离，y 相对竖直中心
    public static void LeftMiddle(RectTransform rt, float x, float y, float w, float h)
    {
        Place(rt, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(w, h));
    }

    public static Sprite SpriteFrom(Texture2D tex)
    {
        return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f));
    }

    // 整圆环
    public static Texture2D MakeRingTexture(int size, float innerRatio, float outerRatio)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        var px = new Color[size * size];
        float c = (size - 1) * 0.5f;
        float rIn = innerRatio * c;
        float rOut = outerRatio * c;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - c, dy = y - c;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01((rOut - d) / 1.5f) * Mathf.Clamp01((d - rIn) / 1.5f);
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    // 圆弧：以"正上方"为中心，向两侧各展开 halfAngleDeg 度；用旋转摆放。
    public static Texture2D MakeArcTexture(int size, float innerRatio, float outerRatio, float halfAngleDeg)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        var px = new Color[size * size];
        float c = (size - 1) * 0.5f;
        float rIn = innerRatio * c;
        float rOut = outerRatio * c;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - c, dy = y - c;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg; // 90 = 上
                float diff = Mathf.Abs(Mathf.DeltaAngle(90f, ang));
                float inAngle = diff <= halfAngleDeg ? 1f : 0f;
                float radial = Mathf.Clamp01((rOut - d) / 1.5f) * Mathf.Clamp01((d - rIn) / 1.5f);
                px[y * size + x] = new Color(1f, 1f, 1f, inAngle * radial);
            }
        }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    // 实心圆（用于指针/光标/点数标记）
    public static Texture2D MakeCircleTexture(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        var px = new Color[size * size];
        float c = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - c, dy = y - c;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01((c - d) / 1.5f);
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }
}
