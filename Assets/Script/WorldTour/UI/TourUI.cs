using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class TourUI
{
    public static readonly Color Gold = new Color(1f, 0.78f, 0.18f);
    public static readonly Color Cyan = new Color(0.3f, 0.76f, 1f);
    public static readonly Color Navy = new Color(0.015f, 0.055f, 0.16f);

    public static RectTransform Canvas(Transform parent, string name, int order)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        Canvas canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = order;
        CanvasScaler scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        if (Object.FindFirstObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule)).transform.SetParent(go.transform, false);
        return (RectTransform)go.transform;
    }

    public static RectTransform Rect(Transform parent, string name, float x, float y, float width, float height)
    {
        var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rect.gameObject.layer = 5;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }

    public static TourPanelGraphic Panel(Transform parent, string name, float x, float y, float width, float height, bool gold = false)
    {
        TourPanelGraphic panel = Rect(parent, name, x, y, width, height).gameObject.AddComponent<TourPanelGraphic>();
        panel.raycastTarget = false;
        panel.BorderColor = gold ? Gold : Cyan;
        panel.BorderWidth = gold ? 4f : 2f;
        return panel;
    }

    public static TMP_Text Text(Transform parent, string name, string value, float x, float y, float width, float height,
        float size, TMP_FontAsset font, Color? tint = null, bool center = false)
    {
        TextMeshProUGUI text = Rect(parent, name, x, y, width, height).gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.text = value;
        text.fontSize = size;
        text.fontStyle = FontStyles.Bold;
        text.color = tint ?? Color.white;
        text.alignment = center ? TextAlignmentOptions.Center : TextAlignmentOptions.MidlineLeft;
        text.enableAutoSizing = true;
        text.fontSizeMax = size;
        text.fontSizeMin = Mathf.Min(size, 15f);
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        return text;
    }

    public static TourIconGraphic Icon(Transform parent, string name, TourIconGraphic.Symbol shape,
        float x, float y, float size, bool lit = true)
    {
        TourIconGraphic icon = Rect(parent, name, x, y, size, size).gameObject.AddComponent<TourIconGraphic>();
        icon.Shape = shape;
        icon.Lit = lit;
        icon.raycastTarget = false;
        return icon;
    }

    public static RawImage Picture(Transform parent, string name, Texture texture, float x, float y, float width, float height)
    {
        RawImage image = Rect(parent, name, x, y, width, height).gameObject.AddComponent<RawImage>();
        image.texture = texture;
        image.raycastTarget = false;
        if (texture != null)
        {
            float sourceAspect = texture.width / (float)texture.height;
            float targetAspect = width / height;
            if (sourceAspect > targetAspect)
            {
                float crop = targetAspect / sourceAspect;
                image.uvRect = new Rect((1f - crop) * 0.5f, 0f, crop, 1f);
            }
            else
            {
                float crop = sourceAspect / targetAspect;
                image.uvRect = new Rect(0f, (1f - crop) * 0.5f, 1f, crop);
            }
        }
        return image;
    }

    public static Button Button(Transform parent, string name, string title, string caption,
        float x, float y, float width, UnityAction action, WorldTourUITheme theme, Color? top = null)
    {
        TourPanelGraphic surface = Panel(parent, name, x, y, width, 100f, true);
        surface.TopColor = top ?? new Color(1f, 0.88f, 0.32f);
        surface.BottomColor = top.HasValue ? top.Value * new Color(0.65f, 0.65f, 0.65f, 1f) : new Color(0.95f, 0.54f, 0.025f);
        surface.raycastTarget = true;
        Button button = surface.gameObject.AddComponent<Button>();
        button.targetGraphic = surface;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(1.13f, 1.13f, 1.13f);
        colors.pressedColor = new Color(0.72f, 0.8f, 0.9f);
        colors.disabledColor = new Color(0.46f, 0.5f, 0.6f, 0.8f);
        button.colors = colors;
        if (action != null) button.onClick.AddListener(action);
        Color label = top.HasValue ? Color.white : Navy;
        Text(surface.transform, "Title", title, 15f, 8f, width - 30f, 52f, 43f, theme.DisplayFont, label, true);
        Text(surface.transform, "Caption", caption, 15f, 59f, width - 30f, 28f, 20f, theme.JapaneseFont, label, true);
        return button;
    }

    public static void Logo(Transform parent, WorldTourUITheme theme)
    {
        Panel(parent, "Logo Plate", 32f, 24f, 800f, 144f);
        Text(parent, "Logo", "WORLD <color=#FFD345>TOUR</color>", 54f, 15f, 760f, 112f, 104f, theme.DisplayFont);
        Text(parent, "Logo Caption", "ワールドツアー   HOME RUNS AROUND THE WORLD", 73f, 124f, 730f, 27f, 20f, theme.JapaneseFont);
    }

    public static void Totals(Transform parent, WorldTourUITheme theme, WorldTourManager manager)
    {
        Panel(parent, "Player Totals", 1325f, 30f, 555f, 94f);
        Icon(parent, "Total Star", TourIconGraphic.Symbol.Star, 1347f, 43f, 65f);
        int earned = 0, total = 0;
        if (manager?.Config != null)
        foreach (WorldData world in manager.Config.Worlds)
        {
            if (string.IsNullOrEmpty(world.WorldId)) continue;
            earned += WorldTourMenuView.WorldStars(world, manager.SaveData);
            total += world.Stages.Count * 3;
        }
        Text(parent, "Stars Label", "STARS", 1423f, 38f, 170f, 26f, 23f, theme.DisplayFont);
        Text(parent, "Stars Count", $"{earned} / {total}", 1423f, 65f, 170f, 47f, 38f, theme.DisplayFont, Gold);
        Icon(parent, "Points Icon", TourIconGraphic.Symbol.Coin, 1613f, 45f, 59f);
        Text(parent, "Points Label", "POINTS", 1684f, 38f, 175f, 26f, 23f, theme.DisplayFont);
        Text(parent, "Points Count", (manager?.SaveData.Points ?? 0).ToString("N0"), 1684f, 65f, 175f, 47f, 38f, theme.DisplayFont);
    }
}
