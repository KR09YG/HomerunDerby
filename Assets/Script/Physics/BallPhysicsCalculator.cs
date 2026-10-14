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
    public PitchLaunchMode LaunchMode;
    // Directionではワールド座標の方向を指定する。大きさは球速の設定値から決まる。
    public Vector3 LaunchDirection;
}

public enum PitchLaunchMode { TargetPoint, Direction }

public static class BallPhysicsCalculator
{
    public struct SimulationConfig
    {
        // 時間刻みは秒単位で指定する。
        public float DeltaTime;
        public float MaxSimulationTimeSeconds;
        public float? StopAtZ;
        public BounceSettings BounceSettings;
        public SpinState? PitchSpin;
        // 打球では、現在の球速と有効回転数から各ステップの揚力係数を求める。
        public BattingParameters BattingParameters;
    }

    public static List<Vector3> CalculateTrajectory(PitchRequest request)
    {
        return CalculateTrajectory(request, out _);
    }

    public static List<Vector3> CalculateTrajectory(PitchRequest request, out SpinState spin)
    {
        return CalculateTrajectory(request, out spin, out _);
    }

    public static List<Vector3> CalculateTrajectory(PitchRequest request, out SpinState spin, out Vector3 initialVelocityMps)
    {
        float speedMps = request.BallData.Speed * BallPhysicsConstants.KPH_TO_MPS;
        if (!IsFinite(speedMps) || speedMps <= 0f)
            throw new ArgumentOutOfRangeException(nameof(request), "球速は有限値かつ正の値で指定してください。");
        if (!IsFinite(request.ReleasePoint.sqrMagnitude) || !IsFinite(request.StopZ))
            throw new ArgumentException("リリース位置と終了Zには有限値を指定してください。", nameof(request));

        // 目標平面はSolverへ直接渡し、呼び出し元の設定は書き換えない。
        var solverSettings = request.Settings ?? new TrajectorySettings();
        if (!IsFinite(solverSettings.DeltaTime) || solverSettings.DeltaTime <= 0f ||
            !IsFinite(solverSettings.MaxSimulationTime) || solverSettings.MaxSimulationTime <= 0f)
            throw new ArgumentException("時間刻みと最大計算時間は有限値かつ正の値で指定してください。", nameof(request));

        spin = SpinState.Create(request.BallData.SpinAxis, request.BallData.RotateSpeed);
        switch (request.LaunchMode)
        {
            case PitchLaunchMode.TargetPoint:
                var solved = PitchVelocitySolver.SolveInitialVelocity(request.ReleasePoint, request.PassPoint,
                    request.BallData, speedMps, solverSettings, request.BounceSettings);
                if (!solved.Converged)
                    Debug.LogWarning($"[投球] {request.BallData.Name}のXY誤差={solved.ErrorMeters:F4}m、反復={solved.Iterations}");
                initialVelocityMps = solved.InitialVelocityMps;
                break;
            case PitchLaunchMode.Direction:
                if (!IsFinite(request.LaunchDirection.sqrMagnitude) || request.LaunchDirection.sqrMagnitude < 1e-12f)
                    throw new ArgumentException("投球方向は有限値の非ゼロベクトルで指定してください。", nameof(request));
                initialVelocityMps = (request.LaunchDirection / Mathf.Sqrt(request.LaunchDirection.sqrMagnitude)) * speedMps;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(request), "未対応の投球方式です。");
        }

        var config = new SimulationConfig
        {
            DeltaTime = solverSettings.DeltaTime,
            MaxSimulationTimeSeconds = solverSettings.MaxSimulationTime,
            StopAtZ = request.StopZ,
            BounceSettings = request.BounceSettings,
            PitchSpin = spin
        };

        List<Vector3> trajectory = SimulateTrajectory(
            request.ReleasePoint,
            initialVelocityMps,
            Vector3.zero,
            0f,
            0f,
            config
        );

        return trajectory;
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

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
