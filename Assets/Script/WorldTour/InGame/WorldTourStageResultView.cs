using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class WorldTourStageResultView
{
    public static void Create(Transform parent, StageSessionData session, TMP_FontAsset font, int newStarCount = 0, bool[] previousStars = null)
    {
        if (session == null) return;
        WorldTourUITheme theme = WorldTourUITheme.Load();
        if (theme == null)
        {
            Debug.LogError("[WorldTour] リザルトのUIテーマが見つかりません。");
            return;
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
            535, 172, 850, 123, 112, theme.DisplayFont, session.IsCleared ? TourUI.Gold : Color.white, true);
        for (int i = 0; i < 3; i++)
        {
            bool earned = WorldTourMenuView.HasStar(previousStars, i) ||
                (session.IsCleared && WorldTourMenuView.HasStar(session.AchievedStars, i));
            TourUI.Icon(root, "Earned Star " + i, TourIconGraphic.Symbol.Star,
                785 + i * 119, 284, 112, earned);
        }

        TourUI.Panel(root, "Stage Card", 138, 370, 490, 505, true);
        images.SetImage(TourUI.Picture(root, "Stage Illustration", theme.StadiumMap, 148, 380, 470, 350),
            imageUrl);
        TourUI.Panel(root, "Stage Caption", 148, 710, 470, 154);
        TourUI.Text(root, "World Name", manager?.CurrentWorld?.Name ?? "WORLD TOUR", 172, 390, 420, 45, 33, theme.JapaneseFont);
        TourUI.Text(root, "Stage Number", "STAGE " + session.StageId, 168, 723, 430, 39, 36, theme.DisplayFont, TourUI.Gold, true);
        TourUI.Text(root, "Stage Name", stage?.Name ?? session.StageId, 168, 770, 430, 71, 38, theme.JapaneseFont, null, true);

        TourUI.Panel(root, "Current Record", 650, 417, 555, 320);
        TourUI.Text(root, "Current Record Heading", "今回の記録", 674, 430, 500, 46, 31, theme.JapaneseFont);
        Record(root, theme, 492, "ホームラン数", stats.HomeRunCount.ToString());
        Record(root, theme, 547, "最大HR飛距離", $"{stats.MaxHomeRunDistance:F1} m");
        Record(root, theme, 602, "総フェア飛距離", $"{stats.TotalFairDistance:F1} m");
        Record(root, theme, 657, "方向別HR（左 / 中 / 右）", $"{stats.LeftHomeRunCount} / {stats.CenterHomeRunCount} / {stats.RightHomeRunCount}");

        TourPanelGraphic reward = TourUI.Panel(root, "Reward", 650, 758, 555, 117, true);
        reward.TopColor = new Color(1, 0.97f, 0.84f);
        reward.BottomColor = new Color(0.95f, 0.87f, 0.63f);
        TourUI.Icon(root, "New Stars", TourIconGraphic.Symbol.Star, 668, 782, 69);
        TourUI.Text(root, "New Stars Count", $"+ {newStarCount}", 745, 779, 116, 53, 46, theme.DisplayFont, TourUI.Navy);
        TourUI.Text(root, "New Stars Caption", "新しく獲得した星", 674, 837, 215, 24, 18, theme.JapaneseFont, TourUI.Navy);
        TourUI.Icon(root, "Reward Coin", TourIconGraphic.Symbol.Coin, 901, 786, 58);
        TourUI.Text(root, "Player Points", (manager?.SaveData.Points ?? 0).ToString("N0"), 971, 779, 215, 53, 43, theme.DisplayFont, TourUI.Navy);
        TourUI.Text(root, "Player Points Caption", "所持ポイント", 971, 837, 215, 24, 18, theme.JapaneseFont, TourUI.Navy);

        TourUI.Panel(root, "Stage Missions", 1230, 370, 550, 338);
        TourUI.Text(root, "Missions Heading", "ステージミッション", 1255, 380, 500, 48, 32, theme.JapaneseFont);
        for (int i = 0; i < 3; i++)
        {
            StageCondition condition = stage?.StarObjectives != null && i < stage.StarObjectives.Count ? stage.StarObjectives[i] : null;
            float y = 441 + i * 85;
            bool achieved = WorldTourMenuView.HasStar(session.AchievedStars, i);
            bool previouslyEarned = WorldTourMenuView.HasStar(previousStars, i);
            bool newlyEarned = session.IsCleared && achieved && !previouslyEarned;
            string status = previouslyEarned ? "獲得済み" : newlyEarned ? "初獲得" : achieved ? "条件達成（未クリア）" : "未獲得";
            TourUI.Icon(root, "Mission Star " + i, TourIconGraphic.Symbol.Star, 1247, y + 3, 57, previouslyEarned || newlyEarned);
            TourUI.Text(root, "Mission " + i, WorldTourMenuView.ConditionText(condition), 1320, y, 430, 40, 23, theme.JapaneseFont);
            TourUI.Text(root, "Mission Value " + i, ConditionValue(condition, stats) + $"   {(achieved ? "今回達成" : "今回未達成")} / {status}",
                1320, y + 39, 430, 32, 25, theme.JapaneseFont, achieved ? TourUI.Gold : Color.white);
        }
        TourUI.Panel(root, "Best Record", 1230, 731, 550, 144);
        TourUI.Text(root, "Best Record Heading", "ベスト記録", 1255, 742, 500, 42, 29, theme.JapaneseFont);
        TourUI.Text(root, "Best Record Note", "記録なし", 1255, 798, 500, 44, 23, theme.JapaneseFont, new Color(0.7f, 0.81f, 0.93f));

        StageData next = manager?.GetNextStage();
        bool canAdvance = next != null && manager.IsStageUnlocked(next);
        bool nextWorld = stage != null && stage.IsBoss;
        UnityEngine.UI.Button nextButton = TourUI.Button(root, "Next Stage", canAdvance ? (nextWorld ? "NEXT WORLD" : "NEXT STAGE") : "BACK TO MAP",
            canAdvance ? (nextWorld ? "次のワールドへ" : "次のステージへ") : "ステージ選択へ戻る", 245, 944, 450,
            () =>
            {
                if (manager == null) return;
                if (canAdvance) manager.PlayNextStage();
                else manager.OpenStageSelect();
            }, theme);
        nextButton.interactable = manager != null;
        UnityEngine.UI.Button retryButton = TourUI.Button(root, "Retry", "RETRY", "もう一度挑戦する", 735, 944, 450,
            () => { if (manager != null) manager.PlayStage(session.StageId); }, theme, new Color(0.9f, 0.08f, 0.17f));
        retryButton.interactable = manager != null;
        TourUI.Button(root, "Back To Map", "BACK TO MAP", "ステージマップへ戻る", 1225, 944, 450,
            () => { if (manager != null) manager.OpenStageSelect(); else SceneManager.LoadScene("StageSelect"); }, theme, new Color(0.1f, 0.39f, 0.75f));
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private static void Record(Transform root, WorldTourUITheme theme, float y, string label, string value)
    {
        TourUI.Icon(root, label + " Icon", TourIconGraphic.Symbol.Baseball, 667, y + 1, 42);
        TourUI.Text(root, label, label, 724, y, 285, 43, 23, theme.JapaneseFont);
        TourUI.Text(root, label + " Value", value, 1012, y, 175, 43, 36, theme.DisplayFont, TourUI.Gold);
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
