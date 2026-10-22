using System.Collections.Generic;
using System;


/// <summary>
/// ステージプレイ中のセッションを管理するクラス
/// </summary>
[Serializable]
public class StageSessionController
{
    private StageCondition _stageClearCondition;
    private List<StageCondition> _starConditions;
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

        Session.RemainingBalls--;
        if (IsConditionCleared(_stageClearCondition, Session.Statistics))
        {
            Session.IsCleared = true;
        }

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

        bool allStarsAchieved = true;

        foreach (bool achieved in Session.AchievedStars)
        {
            if (!achieved)
            {
                allStarsAchieved = false;
                break;
            }
        }

        // 全条件達成時は残り球数があっても終了する。
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
        switch (condition.Type)
        {
            case ChallengeConditionType.HomeRunCount:
                return statistics.HomeRunCount >= condition.TargetValue;
            case ChallengeConditionType.MaxHomeRunDistance:
                return statistics.MaxHomeRunDistance >= condition.TargetValue;
            case ChallengeConditionType.TotalFairDistance:
                return statistics.TotalFairDistance >= condition.TargetValue;
            case ChallengeConditionType.DirectionalHomeRunCount:
                switch (condition.Direction)
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
