using System;
using System.Collections.Generic;

[Serializable]
public class StageData
{
    public string StageId;
    public string Name;

    public int BallCount;
    public bool IsBoss;

    public StageCondition ClearObjective;
    // 星獲得条件のリスト(どのステージでも星の数は固定で３個)
    public List<StageCondition> StarObjectives = new List<StageCondition>(3);

    // 持ち球のリスト
    public List<StagePitchEntry> PitchPool = new List<StagePitchEntry>(20);

    public int? StarRewardOverride;
}
