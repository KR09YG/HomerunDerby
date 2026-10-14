using System;

[Serializable]
public class StageSessionData
{
    public string StageId;
    // 残りの球数
    public int RemainingBalls;

    // ステージがクリアされたかどうか
    public bool IsCleared;
    // ステージが終了したかどうか
    public bool IsFinished;

    // 達成した星の数を表す配列（3つの星に対応）
    public bool[] AchievedStars;
    // ステージの統計情報
    public StageStatistics Statistics;

    public StageSessionData(StageData stage)
    {
        if (stage == null)
            throw new ArgumentNullException(nameof(stage));

        StageId = stage.StageId;
        RemainingBalls = stage.BallCount;

        IsCleared = false;
        IsFinished = false;

        AchievedStars = new bool[3];
        Statistics = new StageStatistics();
    }
}