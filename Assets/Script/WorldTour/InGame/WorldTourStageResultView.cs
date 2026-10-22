using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System;

public static class WorldTourStageResultView
{
    public sealed class SaveControls
    {
        private readonly RectTransform _root;
        private readonly Button _retry, _back, _retrySave;
        private readonly TMP_Text _status;
        private readonly WorldTourImageLoader _images;
        private bool _saved;

        public bool CanNavigate => _root != null && _saved;
        public bool AreImagesReady => _images == null || !_images.IsLoading;

        internal SaveControls(RectTransform root, Button retry, Button back, Button retrySave, TMP_Text status,
            WorldTourImageLoader images)
        {
            _root = root;
            _retry = retry;
            _back = back;
            _retrySave = retrySave;
            _status = status;
            _images = images;
        }

        public void SetSaving() => SetState(false, false, "保存中…");
        public void SetSaved() => SetState(true, false, "クラウドに保存しました");
        public void SetSaveFailed() => SetState(false, true, "保存に失敗しました。接続を確認して再試行してください。");

        private void SetState(bool saved, bool failed, string message)
        {
            if (_root == null) return;
            _saved = saved;
            _retry.interactable = saved && WorldTourManager.Instance != null;
            _back.interactable = saved;
            _retry.gameObject.SetActive(!failed);
            _retrySave.gameObject.SetActive(failed);
            _status.text = message;
            _status.color = failed ? new Color(1f, .55f, .45f) : Color.white;
        }
    }

    public static SaveControls Create(Transform parent, StageSessionData session, TMP_FontAsset font,
        int newStarCount = 0, bool[] previousStars = null, Action retrySave = null)
    {
        if (session == null) return null;
        WorldTourUITheme theme = WorldTourUITheme.Load();
        if (theme == null)
        {
            Debug.LogError("[WorldTour] リザルトのUIテーマが見つかりません。");
            return null;
        }
        WorldTourManager manager = WorldTourManager.Instance;
        StageData stage = manager?.CurrentStage;
        WorldData world = manager?.CurrentWorld;
        StageStatistics stats = session.Statistics ?? new StageStatistics();
        RectTransform root = TourUI.Canvas(parent, "World Tour Stage Result", 300);
        WorldTourImageLoader images = root.gameObject.AddComponent<WorldTourImageLoader>();
        string imageUrl = string.IsNullOrWhiteSpace(stage?.ImageUrl) ? world?.ImageUrl : stage.ImageUrl;
        images.SetImage(TourUI.Picture(root, "Stadium Background", theme.StadiumMap, 0, 0, 1920, 1080),
            imageUrl);
        TourPanelGraphic shade = TourUI.Panel(root, "Backdrop", 0, 0, 1920, 1080);
        shade.TopColor = new Color(0.01f, 0.04f, 0.16f, 0.22f);
        shade.BottomColor = new Color(0.01f, 0.03f, 0.1f, 0.65f);
        shade.BorderWidth = 0;
        shade.raycastTarget = true;
        TourUI.Logo(root, theme);
        TourUI.Totals(root, theme, manager);

        TourUI.Panel(root, "Result Frame", 110, 330, 1700, 575, true);
        TourUI.Panel(root, "Result Banner", 505, 165, 910, 145, true);
        TourUI.Text(root, "Result Title", session.IsCleared ? "STAGE CLEAR" : "STAGE FAILED",
            532, 185, 600, 100, 80, theme.DisplayFont, session.IsCleared ? TourUI.Gold : Color.white, true);
        for (int i = 0; i < StageData.StarCount; i++)
        {
            bool earned = WorldTourMenuView.HasStar(previousStars, i) ||
                (session.IsCleared && WorldTourMenuView.HasStar(session.AchievedStars, i));
            TourUI.Icon(root, "Earned Star " + i, TourIconGraphic.Symbol.Star,
                1148 + i * 89, 200, 76, earned);
        }

        TourUI.Panel(root, "Stage Card", 138, 370, 490, 505, true);
        images.SetImage(TourUI.Picture(root, "Stage Illustration", theme.StadiumMap, 148, 380, 470, 320),
            imageUrl);
        TourUI.Panel(root, "Stage Caption", 148, 710, 470, 154);
        ReadableText(root, "World Name", manager?.CurrentWorld?.Name ?? "WORLD TOUR", 172, 390, 420, 45, 28, theme.JapaneseFont);
        TourUI.Text(root, "Stage Number", "STAGE " + session.StageId, 168, 723, 430, 39, 36, theme.DisplayFont, TourUI.Gold, true);
        ReadableText(root, "Stage Name", stage?.Name ?? session.StageId, 168, 770, 430, 71, 30, theme.JapaneseFont, null, true);

        TourUI.Panel(root, "Current Record", 650, 370, 555, 365);
        ReadableText(root, "Current Record Heading", "今回の記録", 674, 386, 500, 46, 30, theme.JapaneseFont);
        Record(root, theme, 450, "ホームラン数", stats.HomeRunCount.ToString());
        Record(root, theme, 511, "最大HR飛距離", $"{stats.MaxHomeRunDistance:F1} m");
        Record(root, theme, 572, "総フェア飛距離", $"{stats.TotalFairDistance:F1} m");
        Record(root, theme, 633, "方向別HR（左 / 中 / 右）", $"{stats.LeftHomeRunCount} / {stats.CenterHomeRunCount} / {stats.RightHomeRunCount}");

        TourPanelGraphic reward = TourUI.Panel(root, "Reward", 650, 758, 555, 117, true);
        reward.TopColor = new Color(1, 0.97f, 0.84f);
        reward.BottomColor = new Color(0.95f, 0.87f, 0.63f);
        TourUI.Icon(root, "New Stars", TourIconGraphic.Symbol.Star, 815, 778, 65);
        TourUI.Text(root, "New Stars Count", $"+ {newStarCount}", 898, 779, 150, 53, 46, theme.DisplayFont, TourUI.Navy);
        ReadableText(root, "New Stars Caption", "新しく獲得した星", 760, 838, 340, 27, 20, theme.JapaneseFont, TourUI.Navy, true);

        TourUI.Panel(root, "Stage Missions", 1230, 370, 550, 338);
        ReadableText(root, "Missions Heading", "ステージミッション", 1255, 380, 500, 48, 30, theme.JapaneseFont);
        for (int i = 0; i < StageData.StarCount; i++)
        {
            StageCondition condition = stage?.StarObjectives != null && i < stage.StarObjectives.Count ? stage.StarObjectives[i] : null;
            float y = 445 + i * 80;
            bool achieved = WorldTourMenuView.HasStar(session.AchievedStars, i);
            bool previouslyEarned = WorldTourMenuView.HasStar(previousStars, i);
            bool newlyEarned = session.IsCleared && achieved && !previouslyEarned;
            string status;
            if (previouslyEarned) status = achieved ? "今回達成・獲得済み" : "獲得済み";
            else if (newlyEarned) status = "今回初獲得";
            else status = achieved ? "今回達成（未クリア）" : "未獲得";
            TourUI.Icon(root, "Mission Star " + i, TourIconGraphic.Symbol.Star, 1249, y + 4, 50, previouslyEarned || newlyEarned);
            ReadableText(root, "Mission " + i, WorldTourMenuView.ConditionText(condition), 1315, y, 435, 34, 22, theme.JapaneseFont);
            ReadableText(root, "Mission Value " + i, ConditionValue(condition, stats),
                1315, y + 37, 150, 28, 21, theme.JapaneseFont, achieved ? TourUI.Gold : Color.white);
            ReadableText(root, "Mission Status " + i, status,
                1472, y + 37, 278, 28, 19, theme.JapaneseFont, achieved ? TourUI.Gold : Color.white);
        }
        TourUI.Panel(root, "Best Record", 1230, 731, 550, 144);
        ReadableText(root, "Best Record Heading", "ベスト記録", 1255, 742, 500, 42, 28, theme.JapaneseFont);
        ReadableText(root, "Best Record Note", "記録なし", 1255, 798, 500, 44, 22, theme.JapaneseFont, new Color(0.7f, 0.81f, 0.93f));

        SaveControls controls = null;
        Button retryButton = TourUI.Button(root, "Retry", "RETRY", "もう一度挑戦する", 490, 944, 450,
            () => { if (controls != null && controls.CanNavigate && manager != null) manager.PlayStage(session.StageId); }, theme, new Color(0.9f, 0.08f, 0.17f));
        Button backButton = TourUI.Button(root, "Back To Map", "BACK TO MAP", "ステージマップへ戻る", 980, 944, 450,
            () =>
            {
                if (controls == null || !controls.CanNavigate) return;
                if (manager != null) manager.OpenStageSelect(); else SceneManager.LoadScene("StageSelect");
            }, theme, new Color(0.1f, 0.39f, 0.75f));
        Button saveButton = TourUI.Button(root, "Retry Save", "RETRY SAVE", "保存を再試行する", 490, 944, 450,
            () => retrySave?.Invoke(), theme, new Color(0.9f, 0.08f, 0.17f));
        TMP_Text saveStatus = TourUI.Text(root, "Save Status", "保存中…", 490, 906, 940, 32, 23, theme.JapaneseFont);
        controls = new SaveControls(root, retryButton, backButton, saveButton, saveStatus, images);
        controls.SetSaving();
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        return controls;
    }

    private static void Record(Transform root, WorldTourUITheme theme, float y, string label, string value)
    {
        TourUI.Icon(root, label + " Icon", TourIconGraphic.Symbol.Baseball, 670, y + 2, 38);
        ReadableText(root, label, label, 720, y, 300, 43,
            label.StartsWith("方向別", StringComparison.Ordinal) ? 19 : 22, theme.JapaneseFont);
        TourUI.Text(root, label + " Value", value, 1030, y, 155, 43, 30, theme.DisplayFont, TourUI.Gold);
    }

    private static TMP_Text ReadableText(Transform root, string name, string value, float x, float y,
        float width, float height, float size, TMP_FontAsset font, Color? color = null, bool center = false)
    {
        TMP_Text text = TourUI.Text(root, name, value, x, y, width, height, size, font, color, center);
        text.fontStyle = FontStyles.Normal;
        text.fontSizeMin = Mathf.Max(17f, size - 3f);
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }

    private static string ConditionValue(StageCondition condition, StageStatistics stats)
    {
        if (condition == null) return "—";
        float value;
        switch (condition.Type)
        {
            case ChallengeConditionType.HomeRunCount: value = stats.HomeRunCount; break;
            case ChallengeConditionType.MaxHomeRunDistance: value = stats.MaxHomeRunDistance; break;
            case ChallengeConditionType.TotalFairDistance: value = stats.TotalFairDistance; break;
            case ChallengeConditionType.DirectionalHomeRunCount:
                value = condition.Direction == HomeRunDirection.Left ? stats.LeftHomeRunCount : condition.Direction == HomeRunDirection.Right ? stats.RightHomeRunCount : stats.CenterHomeRunCount;
                break;
            default: return "—";
        }
        return $"{value:0.#} / {condition.TargetValue:0.#}";
    }
}
