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
    // リリース時の総回転数に対する、進行方向と直交する回転成分の割合。
    [Range(0, 1)]
    public float SpinEfficiency;
}

/// <summary>
/// リリース時の総角速度をワールド座標で保持する。飛翔中の回転減衰と空力トルクは省略する。
/// </summary>
public readonly struct SpinState
{
    public Vector3 AngularVelocityRadPerSec { get; }
    public float TotalSpinRateRpm => AngularVelocityRadPerSec.magnitude / BallPhysicsConstants.RPM_TO_RAD_PER_SEC;

    private SpinState(Vector3 angularVelocityRadPerSec)
    {
        AngularVelocityRadPerSec = angularVelocityRadPerSec;
    }

    public static SpinState Create(Vector3 initialVelocityMps, float totalSpinRateRpm, float spinTiltDeg, float releaseSpinEfficiency)
    {
        if (initialVelocityMps.sqrMagnitude < 1e-12f)
            throw new ArgumentException("回転軸の生成には初速が必要です。", nameof(initialVelocityMps));
        if (float.IsNaN(releaseSpinEfficiency) || releaseSpinEfficiency < 0f || releaseSpinEfficiency > 1f)
            throw new ArgumentOutOfRangeException(nameof(releaseSpinEfficiency), "回転効率は0から1で指定してください。");
        if (float.IsNaN(totalSpinRateRpm) || float.IsInfinity(totalSpinRateRpm) || totalSpinRateRpm < 0f)
            throw new ArgumentOutOfRangeException(nameof(totalSpinRateRpm));

        Vector3 forward = initialVelocityMps.normalized;
        Vector3 right = Vector3.ProjectOnPlane(Vector3.right, forward);
        if (right.sqrMagnitude < 1e-6f)
            right = Vector3.ProjectOnPlane(Vector3.up, forward);
        right.Normalize();
        Vector3 up = Vector3.Cross(forward, right).normalized;
        float tiltRad = spinTiltDeg * Mathf.Deg2Rad;
        Vector3 transverseAxis = (Mathf.Cos(tiltRad) * right + Mathf.Sin(tiltRad) * up).normalized;
        float gyroRatio = Mathf.Sqrt(Mathf.Max(0f, 1f - releaseSpinEfficiency * releaseSpinEfficiency));
        float angularSpeedRadPerSec = totalSpinRateRpm * BallPhysicsConstants.RPM_TO_RAD_PER_SEC;
        // 現在の球種データにはジャイロ成分の符号がないため、初速と同じ向きにする。
        return new SpinState(angularSpeedRadPerSec * (transverseAxis * releaseSpinEfficiency + forward * gyroRatio));
    }

    public Vector3 GetTransverseAngularVelocity(Vector3 velocityMps)
    {
        if (velocityMps.sqrMagnitude < 1e-12f) return Vector3.zero;
        Vector3 forward = velocityMps.normalized;
        return AngularVelocityRadPerSec - Vector3.Dot(AngularVelocityRadPerSec, forward) * forward;
    }

    public Quaternion RotationAfter(float seconds)
    {
        float angularSpeedRadPerSec = AngularVelocityRadPerSec.magnitude;
        if (angularSpeedRadPerSec < 1e-8f) return Quaternion.identity;
        return Quaternion.AngleAxis(angularSpeedRadPerSec * Mathf.Rad2Deg * seconds, AngularVelocityRadPerSec / angularSpeedRadPerSec);
    }
}

public static class BallPhysicsCalculator
{
    public struct SimulationConfig
    {
        // 時間刻みは秒単位で指定する。
        public float DeltaTime;
        public float MaxSimulationTimeSeconds;
        public float? StopAtZ;
        public BounceSettings BounceSettings;
        public string GroundLayer;
        public SpinState? PitchSpin;
    }

    public static List<Vector3> CalculateTrajectory(PitchRequest request)
    {
        return CalculateTrajectory(request, out _);
    }

    public static List<Vector3> CalculateTrajectory(PitchRequest request, out SpinState spin)
    {
        Debug.Log("========== 軌道計算開始 ==========");

        float speedMps = request.BallData.Speed * BallPhysicsConstants.KPH_TO_MPS;

        // PassPointを終点として最適化
        var solverSettings = request.Settings ?? new TrajectorySettings();
        solverSettings.StopPosition = request.PassPoint;
        solverSettings.StopAtTarget = true;

        Vector3 optimalVelocityMps = PitchVelocitySolver.FindOptimalVelocityAdvanced(
            request.ReleasePoint,
            request.PassPoint,
            request.BallData,
            speedMps,
            solverSettings,
            request.BounceSettings
        );

        spin = SpinState.Create(optimalVelocityMps, request.BallData.RotateSpeed,
            request.BallData.SpinTilt, request.BallData.SpinEfficiency);

        // StopZまで軌道を計算（表示用）
        var config = new SimulationConfig
        {
            DeltaTime = solverSettings.DeltaTime != 0 ? solverSettings.DeltaTime : 0.01f,
            MaxSimulationTimeSeconds = solverSettings.MaxSimulationTime != 0 ? solverSettings.MaxSimulationTime : 5f,
            StopAtZ = request.StopZ,
            BounceSettings = request.BounceSettings,
            PitchSpin = spin
        };

        List<Vector3> trajectory = SimulateTrajectory(
            request.ReleasePoint,
            optimalVelocityMps,
            Vector3.zero,
            0f,
            0f,
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
    /// ワールドXY平面上の有効回転軸を求める。0度は+X、90度は+Y方向。
    /// </summary>
    public static Vector3 ToSpinAxis(float spinTiltDeg)
    {
        float spinTiltRad = spinTiltDeg * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(spinTiltRad), Mathf.Sin(spinTiltRad), 0f).normalized;
    }

    /// <summary>進行方向に直交する回転数（rpm）と球速（m/s）から揚力係数を求める。</summary>
    public static float CalcCl(float speedMps, float transverseSpinRateRpm)
    {
        float transverseAngularSpeedRadPerSec = transverseSpinRateRpm * BallPhysicsConstants.RPM_TO_RAD_PER_SEC;
        float spinParam = speedMps > 0f
            ? (BallPhysicsConstants.BALL_RADIUS_M * transverseAngularSpeedRadPerSec) / speedMps
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
