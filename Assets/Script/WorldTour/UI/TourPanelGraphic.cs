using UnityEngine;
using UnityEngine.UI;

// 解像度を変えても輪郭がぼけないよう、パネルと縁をメッシュで描く。
[ExecuteAlways, RequireComponent(typeof(CanvasRenderer))]
public sealed class TourPanelGraphic : MaskableGraphic
{
    public Color TopColor = new Color(0.02f, 0.19f, 0.43f, 0.98f);
    public Color BottomColor = new Color(0.01f, 0.04f, 0.13f, 0.98f);
    public Color BorderColor = new Color(0.28f, 0.75f, 1f);
    public float BorderWidth = 2f;
    public float CornerSize = 16f;

    protected override void OnEnable()
    {
        useLegacyMeshGeneration = false;
        base.OnEnable();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;
        float cut = Mathf.Min(CornerSize, Mathf.Min(r.width, r.height) * 0.25f);
        Vector2[] outer = Corners(r, cut);
        float border = Mathf.Clamp(BorderWidth, 0f, Mathf.Min(r.width, r.height) * 0.2f);
        Rect inside = new Rect(r.x + border, r.y + border, r.width - border * 2f, r.height - border * 2f);
        Vector2[] inner = Corners(inside, Mathf.Max(0f, cut - border * 0.5f));
        for (int i = 0; i < 8; i++)
        {
            vh.AddVert(outer[i], BorderColor * color, Vector2.zero);
            vh.AddVert(inner[i], Color.Lerp(BottomColor, TopColor, Mathf.InverseLerp(r.yMin, r.yMax, inner[i].y)) * color, Vector2.zero);
        }
        vh.AddVert(r.center, Color.Lerp(BottomColor, TopColor, 0.5f) * color, Vector2.zero);
        for (int i = 0; i < 8; i++)
        {
            int next = (i + 1) % 8;
            vh.AddTriangle(i * 2, next * 2, next * 2 + 1);
            vh.AddTriangle(i * 2, next * 2 + 1, i * 2 + 1);
            vh.AddTriangle(16, i * 2 + 1, next * 2 + 1);
        }
    }

    private static Vector2[] Corners(Rect r, float c) => new[]
    {
        new Vector2(r.xMin + c, r.yMin), new Vector2(r.xMax - c, r.yMin),
        new Vector2(r.xMax, r.yMin + c), new Vector2(r.xMax, r.yMax - c),
        new Vector2(r.xMax - c, r.yMax), new Vector2(r.xMin + c, r.yMax),
        new Vector2(r.xMin, r.yMax - c), new Vector2(r.xMin, r.yMin + c)
    };
}
