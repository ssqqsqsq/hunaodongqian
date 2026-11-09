using UnityEngine;
using UnityEngine.UI;

// 运行时绘制的「扇形/环形扇区」UI 图元，用于仪表盘的灰色余量填充。
// 角度以 +X（3 点钟方向）为 0°，逆时针为正。
[RequireComponent(typeof(CanvasRenderer))]
public class RadialSectorGraphic : MaskableGraphic
{
    [SerializeField] private float startAngle;   // 起始角（度）
    [SerializeField] private float sweepAngle = 180f; // 逆时针扫过的角度（度）
    [SerializeField, Range(0f, 0.98f)] private float innerRadius; // 内半径占比（0=实心扇形）

    private const int SegmentsPer180 = 64;

    public float StartAngle
    {
        get => startAngle;
        set { if (!Mathf.Approximately(startAngle, value)) { startAngle = value; SetVerticesDirty(); } }
    }

    public float SweepAngle
    {
        get => sweepAngle;
        set { if (!Mathf.Approximately(sweepAngle, value)) { sweepAngle = value; SetVerticesDirty(); } }
    }

    public float InnerRadius
    {
        get => innerRadius;
        set { if (!Mathf.Approximately(innerRadius, value)) { innerRadius = value; SetVerticesDirty(); } }
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Rect r = GetPixelAdjustedRect();
        Vector2 center = r.center;
        float outer = Mathf.Min(r.width, r.height) * 0.5f;
        float inner = outer * Mathf.Clamp01(innerRadius);

        float sweep = sweepAngle;
        int seg = Mathf.Max(2, Mathf.CeilToInt(Mathf.Abs(sweep) / 180f * SegmentsPer180));
        float step = sweep / seg;
        Color32 c = color;

        for (int i = 0; i < seg; i++)
        {
            float a0 = (startAngle + step * i) * Mathf.Deg2Rad;
            float a1 = (startAngle + step * (i + 1)) * Mathf.Deg2Rad;

            Vector2 dir0 = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0));
            Vector2 dir1 = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1));

            Vector2 o0 = center + dir0 * outer;
            Vector2 o1 = center + dir1 * outer;

            if (inner <= 0.01f)
            {
                AddTri(vh, center, o0, o1, c);
            }
            else
            {
                Vector2 i0 = center + dir0 * inner;
                Vector2 i1 = center + dir1 * inner;
                AddQuad(vh, i0, o0, o1, i1, c);
            }
        }
    }

    private static void AddTri(VertexHelper vh, Vector2 a, Vector2 b, Vector2 d, Color32 c)
    {
        int baseIndex = vh.currentVertCount;
        vh.AddVert(a, c, Vector2.zero);
        vh.AddVert(b, c, Vector2.zero);
        vh.AddVert(d, c, Vector2.zero);
        vh.AddTriangle(baseIndex, baseIndex + 1, baseIndex + 2);
    }

    private static void AddQuad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c2, Vector2 d, Color32 c)
    {
        int baseIndex = vh.currentVertCount;
        vh.AddVert(a, c, Vector2.zero);
        vh.AddVert(b, c, Vector2.zero);
        vh.AddVert(c2, c, Vector2.zero);
        vh.AddVert(d, c, Vector2.zero);
        vh.AddTriangle(baseIndex, baseIndex + 1, baseIndex + 2);
        vh.AddTriangle(baseIndex + 2, baseIndex + 3, baseIndex);
    }
}
