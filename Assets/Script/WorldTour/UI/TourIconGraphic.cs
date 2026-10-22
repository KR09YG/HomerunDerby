using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways, RequireComponent(typeof(CanvasRenderer))]
public sealed class TourIconGraphic : MaskableGraphic
{
    public enum Symbol { Star, Baseball, Lock, Arrow, Coin, Crown, Dot }
    public Symbol Shape;
    public bool Lit = true;
    private static readonly Color Gold = new Color(1f, 0.77f, 0.16f);

    protected override void OnEnable()
    {
        useLegacyMeshGeneration = false;
        base.OnEnable();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;
        Vector2 center = r.center;
        float radius = Mathf.Min(r.width, r.height) * 0.45f;
        Color ink = Lit ? Gold : new Color(0.35f, 0.45f, 0.56f);
        if (Shape == Symbol.Dot)
        {
            Circle(vh, center, radius, new Color(0.2f, 0.7f, 1f));
            Circle(vh, center, radius * 0.65f, Color.white);
            return;
        }
        if (Shape == Symbol.Star)
        {
            Vector2[] points = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float a = (90f + i * 36f) * Mathf.Deg2Rad;
                points[i] = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius * (i % 2 == 0 ? 1f : 0.46f);
            }
            Polygon(vh, points, center, new Color(0.01f, 0.04f, 0.13f));
            for (int i = 0; i < points.Length; i++) points[i] = center + (points[i] - center) * 0.88f;
            Polygon(vh, points, center, ink);
            return;
        }
        if (Shape == Symbol.Baseball || Shape == Symbol.Coin)
        {
            Circle(vh, center, radius, new Color(0.01f, 0.04f, 0.13f));
            Circle(vh, center, radius * 0.9f, Shape == Symbol.Coin ? Gold : new Color(0.94f, 0.97f, 1f));
            if (Shape == Symbol.Coin)
            {
                Circle(vh, center, radius * 0.72f, new Color(0.72f, 0.39f, 0.02f));
                Circle(vh, center, radius * 0.64f, Gold);
                Line(vh, center + Vector2.down * radius * 0.45f, center + Vector2.up * radius * 0.45f, radius * 0.16f, Color.white);
            }
            else
            {
                for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < 12; i++)
                {
                    float y = Mathf.Lerp(-0.7f, 0.7f, i / 11f) * radius;
                    float x = side * radius * (0.28f + 0.28f * (y / radius) * (y / radius));
                    Line(vh, center + new Vector2(x - radius * 0.09f, y - radius * 0.035f),
                        center + new Vector2(x + radius * 0.09f, y + radius * 0.035f), radius * 0.035f, new Color(0.8f, 0.08f, 0.15f));
                }
            }
            return;
        }
        if (Shape == Symbol.Lock)
        {
            ink = new Color(0.72f, 0.81f, 0.91f);
            Circle(vh, center + Vector2.up * radius * 0.22f, radius * 0.53f, ink);
            Circle(vh, center + Vector2.up * radius * 0.22f, radius * 0.31f, new Color(0.02f, 0.08f, 0.18f));
            Box(vh, center + Vector2.down * radius * 0.35f, new Vector2(radius * 1.45f, radius * 1.2f), ink);
            Circle(vh, center + Vector2.down * radius * 0.3f, radius * 0.13f, new Color(0.02f, 0.08f, 0.18f));
            return;
        }
        if (Shape == Symbol.Arrow)
        {
            Polygon(vh, new[] { center + new Vector2(-radius, 0f), center + new Vector2(0f, radius),
                center + new Vector2(0f, radius * 0.38f), center + new Vector2(radius, radius * 0.38f),
                center + new Vector2(radius, -radius * 0.38f), center + new Vector2(0f, -radius * 0.38f),
                center + new Vector2(0f, -radius) }, center, color);
            return;
        }
        Polygon(vh, new[] { center + new Vector2(-radius, radius * 0.8f), center + new Vector2(-radius * 0.6f, -radius * 0.7f),
            center + new Vector2(radius * 0.6f, -radius * 0.7f), center + new Vector2(radius, radius * 0.8f),
            center + new Vector2(radius * 0.35f, 0f), center + new Vector2(0f, radius),
            center + new Vector2(-radius * 0.35f, 0f) }, center, Gold);
    }

    private static void Polygon(VertexHelper vh, Vector2[] points, Vector2 center, Color tint)
    {
        int first = vh.currentVertCount;
        vh.AddVert(center, tint, Vector2.zero);
        foreach (Vector2 point in points) vh.AddVert(point, tint, Vector2.zero);
        for (int i = 0; i < points.Length; i++) vh.AddTriangle(first, first + i + 1, first + (i + 1) % points.Length + 1);
    }

    private static void Circle(VertexHelper vh, Vector2 center, float radius, Color tint)
    {
        Vector2[] points = new Vector2[48];
        for (int i = 0; i < points.Length; i++)
        {
            float a = i * Mathf.PI * 2f / points.Length;
            points[i] = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
        }
        Polygon(vh, points, center, tint);
    }

    private static void Box(VertexHelper vh, Vector2 center, Vector2 size, Color tint) => Polygon(vh,
        new[] { center + new Vector2(-size.x, -size.y) * 0.5f, center + new Vector2(size.x, -size.y) * 0.5f,
            center + size * 0.5f, center + new Vector2(-size.x, size.y) * 0.5f }, center, tint);

    private static void Line(VertexHelper vh, Vector2 a, Vector2 b, float width, Color tint)
    {
        Vector2 n = new Vector2(-(b - a).y, (b - a).x).normalized * width * 0.5f;
        Polygon(vh, new[] { a - n, b - n, b + n, a + n }, (a + b) * 0.5f, tint);
    }
}
