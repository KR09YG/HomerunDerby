using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class WorldTourStageResultView
{
    private static readonly Color BackdropColor = new Color(0.015f, 0.025f, 0.04f, 0.48f);
    private static readonly Color PanelColor = new Color(0.025f, 0.04f, 0.065f, 0.94f);
    private static readonly Color RowColor = new Color(0.045f, 0.065f, 0.09f, 0.55f);
    private static readonly Color LineColor = new Color(0.72f, 0.79f, 0.86f, 0.20f);
    private static readonly Color LabelColor = new Color(0.84f, 0.86f, 0.89f, 1f);
    private static readonly Color MutedColor = new Color(0.57f, 0.62f, 0.69f, 1f);
    private static readonly Color GoldColor = new Color(1f, 0.77f, 0.19f, 1f);

    public static void Create(Transform parent, StageSessionData session, TMP_FontAsset font)
    {
        if (session == null) return;

        font ??= TMP_Settings.defaultFontAsset;
        var canvasObject = new GameObject("World Tour Stage Result", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(parent, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 300;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform backdrop = AddRect(canvasObject.transform, "Backdrop", Vector2.zero, Vector2.zero);
        backdrop.anchorMin = Vector2.zero;
        backdrop.anchorMax = Vector2.one;
        backdrop.offsetMin = Vector2.zero;
        backdrop.offsetMax = Vector2.zero;
        AddImage(backdrop.gameObject, BackdropColor);

        RectTransform panel = AddRect(backdrop, "Stage Result Panel", Vector2.zero, new Vector2(1024f, 900f));
        AddImage(panel.gameObject, PanelColor);

        AddText(panel, "Mode", "W O R L D   T O U R", new Vector2(0f, 359f),
            new Vector2(860f, 48f), 30f, MutedColor, TextAlignmentOptions.Center, font);
        AddText(panel, "Stage Result", session.IsCleared ? "STAGE CLEAR!" : "STAGE FAILED",
            new Vector2(0f, 280f), new Vector2(900f, 100f), 78f, Color.white,
            TextAlignmentOptions.Center, font);

        AddLine(panel, 207f, 772f);
        AddImage(AddRect(panel, "Statistics Background", new Vector2(0f, 47f),
            new Vector2(792f, 312f)).gameObject, RowColor);

        AddRow(panel, "ステージ:", session.StageId ?? "—", 159f, font);
        AddRow(panel, "ホームラン数:", session.Statistics.HomeRunCount.ToString(), 81f, font);
        AddRow(panel, "最大飛距離:", $"{session.Statistics.MaxHomeRunDistance:F1} m", 3f, font);
        AddRow(panel, "総フェア飛距離:", $"{session.Statistics.TotalFairDistance:F1} m", -75f, font, false);

        RectTransform starsPanel = AddRect(panel, "Stars", new Vector2(0f, -235f), new Vector2(772f, 112f));
        AddImage(starsPanel.gameObject, RowColor);
        AddText(starsPanel, "Stars Label", "獲得した星:", new Vector2(-232f, 0f),
            new Vector2(285f, 70f), 31f, LabelColor, TextAlignmentOptions.Left, font);
        for (int i = 0; i < 3; i++)
        {
            bool achieved = session.AchievedStars != null &&
                            i < session.AchievedStars.Length && session.AchievedStars[i];
            AddText(starsPanel, $"Star {i + 1}", achieved ? "★" : "☆",
                new Vector2(83f + i * 108f, 0f), new Vector2(86f, 92f), 76f,
                achieved ? GoldColor : MutedColor, TextAlignmentOptions.Center, font);
        }

        AddLine(panel, -355f, 772f);
        AddText(panel, "Footer", "結果確認用の簡易リザルト", new Vector2(0f, -401f),
            new Vector2(772f, 50f), 27f, MutedColor, TextAlignmentOptions.Center, font);
    }

    private static void AddRow(Transform parent, string label, string value, float y, TMP_FontAsset font,
        bool showSeparator = true)
    {
        AddText(parent, label, label, new Vector2(-232f, y), new Vector2(285f, 70f),
            32f, LabelColor, TextAlignmentOptions.Left, font);
        AddText(parent, $"{label} Value", value, new Vector2(222f, y), new Vector2(420f, 70f),
            46f, Color.white, TextAlignmentOptions.Left, font);
        if (showSeparator) AddLine(parent, y - 39f, 772f);
    }

    private static void AddLine(Transform parent, float y, float width)
    {
        AddImage(AddRect(parent, "Separator", new Vector2(0f, y), new Vector2(width, 1f)).gameObject, LineColor);
    }

    private static RectTransform AddRect(Transform parent, string name, Vector2 position, Vector2 size)
    {
        var element = new GameObject(name, typeof(RectTransform));
        RectTransform rect = (RectTransform)element.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static void AddImage(GameObject gameObject, Color color)
    {
        Image image = gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
    }

    private static void AddText(Transform parent, string name, string value, Vector2 position,
        Vector2 size, float fontSize, Color color, TextAlignmentOptions alignment, TMP_FontAsset font)
    {
        RectTransform rect = AddRect(parent, name, position, size);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Bold;
        text.color = color;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
    }
}
