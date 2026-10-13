using UnityEngine;
using System.Collections.Generic;

public class BattingBallResult
{
    // 物理パラメータ
    public Vector3 InitialVelocity;
    public float ExitVelocity;
    public float LaunchAngle;
    public float HorizontalAngle;
    public Vector3 SpinAxis;
    public float SpinRate;

    // インパクト情報
    public float ImpactDistance;
    public float ImpactEfficiency;
    public bool IsSweetSpot;
    public float Timing;

    // 弾道リスト
    public List<Vector3> TrajectoryPoints = new List<Vector3>();
    public BattingBallType BallType;
    public int LandingIndex;
    public float Distance;

    public BattingBallResult(BattingBallType type   )
    {
        BallType = BattingBallType.Miss;
    }
}