using System;
using System.Collections.Generic;

[Serializable]
public class StageData
{
    public string StageId;
    public string Name;
    public string ImageUrl;

    public int BallCount;
    public bool IsBoss;
    public bool IsUnlocked;

    public StageCondition ClearObjective;
    // 星獲得条件のリスト(どのステージでも星の数は固定で３個)
    public List<StageCondition> StarObjectives = new List<StageCondition>(3);

    // 持ち球のリスト
    public List<StagePitchEntry> PitchPool = new List<StagePitchEntry>(20);

    // 今後ステージが分岐するかもしれないのでリストで管理
    public List<string> NextStagesId = new List<string>();

    public int? StarRewardOverride;
}
