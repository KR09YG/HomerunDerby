using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 投球軌道の計算を担当する静的クラス
/// </summary>
public static class PitchTrajectoryCalculator
{
    /// <summary>
    /// 投球軌道を計算
    /// </summary>
    public static List<Vector3> PitchCalculate(
        BallData ballData,
        Vector3 releasePoint,
        Vector3 passPoint,
        float stopZ,
        bool enableDebugLogs = false)
    {
        if (enableDebugLogs)
        {
            Debug.Log("========== 軌道計算開始 ==========");
            Debug.Log($"Release: {releasePoint}");
            Debug.Log($"PassPoint: {passPoint}");
            Debug.Log($"StopZ: {stopZ}");
            Debug.Log($"球種: {ballData.Name}");
        }

        var request = new PitchRequest
        {
            BallData = ballData,
            ReleasePoint = releasePoint,
            PassPoint = passPoint,
            StopZ = stopZ
        };

        List<Vector3> trajectory = BallPhysicsCalculator.CalculateTrajectory(request);

        if (enableDebugLogs)
        {
            LogTrajectoryResults(trajectory);
        }

        if (enableDebugLogs)
        {
            Debug.Log("========== 軌道計算完了 ==========");
        }

        return trajectory;
    }

    /// <summary>
    /// 軌道の総変化量を計算
    /// </summary>
    public static float CalculateTotalCurve(List<Vector3> trajectory)
    {
        if (trajectory == null || trajectory.Count < 3)
        {
            return 0f;
        }

        Vector3 start = trajectory[0];
        Vector3 end = trajectory[trajectory.Count - 1];
        Vector3 straightLine = end - start;
        float maxDeviation = 0f;

        for (int i = 1; i < trajectory.Count - 1; i++)
        {
            Vector3 point = trajectory[i];
            float deviation = Vector3.Cross(straightLine, point - start).magnitude / straightLine.magnitude;
            if (deviation > maxDeviation)
            {
                maxDeviation = deviation;
            }
        }

        return maxDeviation;
    }

    /// <summary>
    /// 軌道計算結果をログ出力
    /// </summary>
    private static void LogTrajectoryResults(List<Vector3> trajectory)
    {
        Debug.Log($"軌道ポイント数: {trajectory.Count}");
        if (trajectory.Count > 0)
        {
            Debug.Log($"軌道開始点: {trajectory[0]}");
            Debug.Log($"軌道終点: {trajectory[trajectory.Count - 1]}");
            float curveAmount = CalculateTotalCurve(trajectory);
            Debug.Log($"変化量: {curveAmount * 100f:F2}cm");
        }
    }
}