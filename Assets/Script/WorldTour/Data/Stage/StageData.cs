using System;
using System.Collections.Generic;

[Serializable]
public class StageData
{
    public const int StarCount = 3;

    public string StageId;
    public string Name;
    public string ImageUrl;

    public int BallCount;
    public bool IsBoss;
    public bool IsUnlocked;

    public StageCondition ClearObjective;
    public List<StageCondition> StarObjectives = new List<StageCondition>(StarCount);

    public List<StagePitchEntry> PitchPool = new List<StagePitchEntry>(20);

    public List<string> NextStagesId = new List<string>();

    public int? StarRewardOverride;
}
