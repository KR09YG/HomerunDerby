using System;
using System.Collections.Generic;
using UnityEngine;

public struct PitchRequest
{
    public BallData BallData;
    public Vector3 ReleasePoint;
    public Vector3 PassPoint;
    public float StopZ;
    public TrajectorySettings Settings;
    public BounceSettings BounceSettings;
}

[Serializable]
public struct BallData
{
    public string Name;
    // 球種の設定値は km/h、rpm、度で保持する。
    public float Speed;
    public float RotateSpeed;
    public float Control;
    public float SpinTilt;
    [Range(0, 1)]
    public float SpinEfficiency;
}

public static class BallPhysicsCalculator
{
    private const float KPH_TO_MPS = 1f / 3.6f;

    public struct SimulationConfig
    {
        // 時間刻みは秒単位で指定する。
        public float DeltaTime;
        public float MaxSimulationTimeSeconds;
        public float? StopAtZ;
        public BounceSettings BounceSettings;
        public string GroundLayer;
    }

    public static List<Vector3> CalculateTrajectory(PitchRequest request)
    {
        Debug.Log("========== 軌道計算開始 ==========");

        float speedMps = request.BallData.Speed * KPH_TO_MPS;

        Vector3 spinAxis = ToSpinAxis(
            request.BallData.SpinTilt,
            request.BallData.SpinEfficiency);

        float liftCoefficient = CalcCl(
            speedMps,
            request.BallData.RotateSpeed * request.BallData.SpinEfficiency);

        // PassPointを終点として最適化
        var solverSettings = request.Settings ?? new TrajectorySettings();
        solverSettings.StopPosition = request.PassPoint;
        solverSettings.StopAtTarget = true;

        Vector3 optimalVelocityMps = PitchVelocitySolver.FindOptimalVelocityAdvanced(
            request.ReleasePoint,
            request.PassPoint,
            spinAxis,
            request.BallData.RotateSpeed * request.BallData.SpinEfficiency,
            liftCoefficient,
            speedMps,
            solverSettings,
            request.BounceSettings
        );

        // StopZまで軌道を計算（表示用）
        var config = new SimulationConfig
        {
            DeltaTime = solverSettings.DeltaTime != 0 ? solverSettings.DeltaTime : 0.01f,
            MaxSimulationTimeSeconds = solverSettings.MaxSimulationTime != 0 ? solverSettings.MaxSimulationTime : 5f,
            StopAtZ = request.StopZ,
            BounceSettings = request.BounceSettings
        };

        List<Vector3> trajectory = SimulateTrajectory(
            request.ReleasePoint,
            optimalVelocityMps,
            spinAxis,
            request.BallData.RotateSpeed * request.BallData.SpinEfficiency,
            liftCoefficient,
            config
        );

        if (trajectory.Count > 0)
        {
            Vector3 endPoint = trajectory[trajectory.Count - 1];
            float error = Vector3.Distance(endPoint, request.PassPoint);
        }

        Debug.Log("========== 軌道計算完了 ==========");
        return trajectory;
    }

    /// <summary>
    /// 回転軸の傾きと有効回転の割合から、正規化した回転軸を求める。
    /// Tilt=0°   X+ → ストレート（上向きマグヌス力）
    /// Tilt=180° X- → カーブ（下向きマグヌス力）
    /// Tilt=90°  Y+ → シュート方向
    /// Tilt=270° Y- → スライダー方向
    /// </summary>
    public static Vector3 ToSpinAxis(float spinTiltDeg, float spinEfficiency)
    {
        float spinTiltRad = spinTiltDeg * Mathf.Deg2Rad;
        float x = Mathf.Cos(spinTiltRad) * spinEfficiency;
        float y = Mathf.Sin(spinTiltRad) * spinEfficiency;
        float z = 1.0f - spinEfficiency;
        Vector3 axis = new Vector3(x, y, z);
        return axis.magnitude > 1e-6f ? axis.normalized : Vector3.forward;
    }

    /// <summary>有効回転数（rpm）と球速（m/s）から揚力係数を求める。</summary>
    public static float CalcCl(float speedMps, float effectiveSpinRateRpm)
    {
        float effectiveAngularVelocityRadPerSec = effectiveSpinRateRpm * BallPhysicsConstants.RPM_TO_RAD_PER_SEC;
        float spinParam = speedMps > 0f
            ? (BallPhysicsConstants.BALL_RADIUS * effectiveAngularVelocityRadPerSec) / speedMps
            : 0f;
        return Mathf.Clamp(
            1.5f * spinParam / (1f + 2.0f * spinParam),
            0f, 0.6f);
    }

    public static List<Vector3> SimulateTrajectory(
        Vector3 startPosition,
        Vector3 initialVelocityMps,
        Vector3 spinAxisNormalized,
        float spinRateRpm,
        float liftCoefficient,
        SimulationConfig config)
    {
        return BallTrajectorySimulator.SimulateTrajectory(
            startPosition, initialVelocityMps, spinAxisNormalized,
            spinRateRpm, liftCoefficient, config);
    }

    public static Vector3 FindPointAtZ(List<Vector3> trajectory, float targetZ)
    {
        if (trajectory == null || trajectory.Count == 0) Debug.Log("tarajectory is none");
        for (int i = 0; i < trajectory.Count - 1; i++)
        {
            Vector3 p1 = trajectory[i];
            Vector3 p2 = trajectory[i + 1];
            if ((p1.z <= targetZ && p2.z >= targetZ) ||
                (p1.z >= targetZ && p2.z <= targetZ))
            {
                float t = Mathf.InverseLerp(p1.z, p2.z, targetZ);
                return Vector3.Lerp(p1, p2, t);
            }
        }
        return trajectory.Count > 0 ? trajectory[trajectory.Count - 1] : Vector3.zero;
    }

    public static (List<Vector3> trajectory, string firstGroundLayer, int landingIndex) SimulateTrajectoryWithGroundInfo(
        Vector3 startPosition,
        Vector3 initialVelocityMps,
        Vector3 spinAxisNormalized,
        float spinRateRpm,
        float liftCoefficient,
        SimulationConfig config)
    {
        var result = BallTrajectorySimulator.SimulateTrajectoryWithMetadata(
            startPosition, initialVelocityMps, spinAxisNormalized,
            spinRateRpm, liftCoefficient, config);
        return (result.Points, result.FirstGroundLayer, result.LandingIndex);
    }
}