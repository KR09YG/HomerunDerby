using System;

[Serializable]
public class StageProgressData
{
    // ステージのクリア状況
    public bool IsCleared;
    // ステージで獲得した星を管理する配列
    public bool[] EarnedStars = new bool[StageData.StarCount];
}
