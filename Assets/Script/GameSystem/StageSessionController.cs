using System.Collections.Generic;
using System;


/// <summary>
/// ステージプレイ中のセッションを管理するクラス
/// </summary>
[Serializable]
public class StageSessionController
{
    private StageCondition _stageClearCondition;
    private List<StageCondition> _starConditions = new List<StageCondition>(3);
    public StageSessionData Session { get; private set; }

    public StageStatistics Initialize(StageData stage)
    {
        Session = new StageSessionData(stage);
        _stageClearCondition = stage.ClearObjective;
        _starConditions = stage.StarObjectives;
        return Session.Statistics;
    }

    public void CompleteAtBat()
    {
        if (Session == null || Session.IsFinished) return;

        // 球数減少
        Session.RemainingBalls--;
        // クリア条件を評価
        if (IsConditionCleared(_stageClearCondition, Session.Statistics))
        {
            Session.IsCleared = true;
        }

        // 星条件を評価
        for (int i = 0; i < _starConditions.Count; i++)
        {
            if (IsConditionCleared(_starConditions[i], Session.Statistics))
            {
                Session.AchievedStars[i] = true;
            }
        }

        ShouldFinishStage();
    }

    public bool ShouldFinishStage()
    {
        if (Session == null) return false;

        // StarAchievedがすべてtrueか判定
        bool allStarsAchieved = true;

        foreach (bool achieved in Session.AchievedStars)
        {
            if (!achieved)
            {
                allStarsAchieved = false;
                break;
            }
        }

        // 残り球数0、またはクリア＋3星
        if (Session.RemainingBalls <= 0 || (Session.IsCleared && allStarsAchieved))
        {
            Session.IsFinished = true;
            return true;
        }
        return false;
    }


    /// <summary>
    /// 指定された条件がクリアされているかを判定する
    /// </summary>
    private bool IsConditionCleared(StageCondition condition, StageStatistics statistics)
    {
        // 条件タイプに応じて判定
        switch (condition.Type)
        {
            case ChallengeConditionType.HomeRunCount:
                return statistics.HomeRunCount >= condition.TargetValue;
            case ChallengeConditionType.MaxHomeRunDistance:
                return statistics.MaxHomeRunDistance >= condition.TargetValue;
            case ChallengeConditionType.TotalFairDistance:
                return statistics.TotalFairDistance >= condition.TargetValue;
            case ChallengeConditionType.DirectionalHomeRunCount:
                switch(condition.Direction)
                {
                    case HomeRunDirection.Left:
                        return statistics.LeftHomeRunCount >= condition.TargetValue;
                    case HomeRunDirection.Center:
                        return statistics.CenterHomeRunCount >= condition.TargetValue;
                    case HomeRunDirection.Right:
                        return statistics.RightHomeRunCount >= condition.TargetValue;
                    default:
                        return false;
                }
            default:
                return false;
        }
    }
}
