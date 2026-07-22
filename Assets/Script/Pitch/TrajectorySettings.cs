using UnityEngine;

/// <summary>
/// シミュレーション設定
/// </summary>
[System.Serializable]
public class TrajectorySettings
{
    [Tooltip("時間刻み幅（小さいほど滑らか、重くなる）")]
    [Range(0.001f, 0.02f)]
    public float DeltaTime = 0.01f;

    public float MaxSimulationTime = 2.0f;
    public bool StopAtTarget = true;
    public Vector3 StopPosition;
    public float StopThreshold = 0.05f;

    public TrajectorySettings()
    {
        StopPosition = Vector3.zero;
    }
}