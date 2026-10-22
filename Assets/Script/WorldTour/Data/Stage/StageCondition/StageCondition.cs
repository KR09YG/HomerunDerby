using System;

[Serializable]
public class StageCondition
{
    public ChallengeConditionType Type;
    public float TargetValue;
    public HomeRunDirection Direction;
}

public enum HomeRunDirection
{
    None,
    Left,
    Right,
    Center
}

public enum ChallengeConditionType
{
    HomeRunCount,
    MaxHomeRunDistance,
    TotalFairDistance,
    DirectionalHomeRunCount
}