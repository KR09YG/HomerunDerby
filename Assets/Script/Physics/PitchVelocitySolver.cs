using System;
using UnityEngine;

public readonly struct PitchSolveResult
{
    public Vector3 InitialVelocityMps { get; }
    /// <summary>目標Z平面上のXY誤差が許容値未満ならtrue。</summary>
    public bool Converged { get; }
    /// <summary>目標Z平面上での左右・高さの誤差の大きさ（m）。</summary>
    public float ErrorMeters { get; }
    /// <summary>外側の反復ループを実行した回数。初期候補の評価は含めない。</summary>
    public int Iterations { get; }

    internal PitchSolveResult(Vector3 initialVelocityMps, bool converged, float errorMeters, int iterations)
    {
        InitialVelocityMps = initialVelocityMps;
        Converged = converged;
        ErrorMeters = errorMeters;
        Iterations = iterations;
    }
}

internal static class PitchVelocitySolver
{
    private const float DIRECTION_PROBE_STEP = 0.001f;
    private const float MAX_DIRECTION_STEP = 0.25f;
    private const float MIN_JACOBIAN_DETERMINANT = 0.000001f;
    private const int MAX_BACKTRACK_STEPS = 6;

    /// <summary>
    /// 設定球速を保ち、目標Z平面での左右・高さのずれが小さくなる投球方向を探す。
    /// </summary>
    internal static PitchSolveResult FindOptimalVelocityAdvanced(
        Vector3 startPoint,
        Vector3 targetPoint,
        BallData ballData,
        float targetSpeedMps,
        TrajectorySettings settings,
        BounceSettings bounceSettings)
    {
        if (float.IsNaN(targetSpeedMps) || float.IsInfinity(targetSpeedMps) || targetSpeedMps <= 0f)
            throw new ArgumentOutOfRangeException(nameof(targetSpeedMps));

        if (!IsFinite(startPoint))
            throw new ArgumentException("リリース位置には有限値を指定してください。", nameof(startPoint));
        if (!IsFinite(targetPoint))
            throw new ArgumentException("目標位置には有限値を指定してください。", nameof(targetPoint));
        if (!IsFinite(ballData.SpinTilt))
            throw new ArgumentException("回転軸の角度には有限値を指定してください。", nameof(ballData));

        float deltaTime = settings?.DeltaTime ?? 0.01f;
        float maxSimulationTime = settings?.MaxSimulationTime ?? 5f;
        if (!IsFinite(deltaTime) || deltaTime <= 0f)
            throw new ArgumentOutOfRangeException(nameof(settings), "時間刻みは有限値かつ正の値で指定してください。");
        if (!IsFinite(maxSimulationTime) || maxSimulationTime <= 0f)
            throw new ArgumentOutOfRangeException(nameof(settings), "最大計算時間は有限値かつ正の値で指定してください。");

        Vector3 displacement = targetPoint - startPoint;
        if (!IsFinite(displacement.sqrMagnitude))
            throw new ArgumentException("リリース位置と目標位置の距離が計算可能な範囲を超えています。", nameof(targetPoint));
        if (displacement.sqrMagnitude < 1e-12f)
            throw new ArgumentException("投球方向を求めるには、リリース位置と目標位置を離してください。", nameof(targetPoint));

        Vector3 forward = displacement.normalized;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        if (right.sqrMagnitude < 1e-6f)
            right = Vector3.Cross(Vector3.right, forward);
        right.Normalize();
        Vector3 up = Vector3.Cross(forward, right).normalized;

        // 最初の方向だけ落下分を見込む。空気力によるずれは軌道を評価して補正する。
        float estimatedTimeSeconds = displacement.magnitude / targetSpeedMps;
        Vector3 initialAim = displacement - Physics.gravity * (0.5f * estimatedTimeSeconds * estimatedTimeSeconds);
        if (!IsFinite(initialAim))
            throw new InvalidOperationException("投球の初期方向を計算中に数値異常が発生しました。");
        float forwardComponent = Vector3.Dot(initialAim, forward);
        Vector2 directionOffset = forwardComponent > 1e-6f
            ? new Vector2(Vector3.Dot(initialAim, right), Vector3.Dot(initialAim, up)) / forwardComponent
            : Vector2.zero;

        bool TryEvaluate(Vector2 offset, int iteration, string candidateName,
            out Vector3 candidateVelocityMps, out Vector2 errorXY)
        {
            string context = $"{candidateName}, Z={targetPoint.z:F3}, 反復={iteration}";
            if (!IsFinite(offset) || !IsFinite(offset.sqrMagnitude))
                throw new InvalidOperationException($"投球方向の補正量が不正です。{context}");
            Vector3 candidateDirection = forward + right * offset.x + up * offset.y;
            if (!IsFinite(candidateDirection.sqrMagnitude) || candidateDirection.sqrMagnitude < 1e-12f)
                throw new InvalidOperationException($"投球方向を正規化できません。{context}");
            candidateVelocityMps = candidateDirection.normalized * targetSpeedMps;
            if (!IsFinite(candidateVelocityMps) || !IsFinite(candidateVelocityMps.magnitude) || candidateVelocityMps.sqrMagnitude == 0f)
                throw new InvalidOperationException($"候補の初速に数値異常が発生しました。{context}");
            SpinState candidateSpin = SpinState.Create(candidateVelocityMps,
                ballData.RotateSpeed, ballData.SpinTilt, ballData.SpinEfficiency);
            if (!IsFinite(candidateSpin.AngularVelocityRadPerSec))
                throw new InvalidOperationException($"候補の回転に数値異常が発生しました。{context}");
            var config = new BallPhysicsCalculator.SimulationConfig
            {
                DeltaTime = deltaTime,
                MaxSimulationTimeSeconds = maxSimulationTime,
                StopAtZ = targetPoint.z,
                BounceSettings = bounceSettings,
                PitchSpin = candidateSpin
            };
            var trajectory = BallTrajectorySimulator.SimulateTrajectory(
                startPoint, candidateVelocityMps, Vector3.zero, 0f, 0f, config);
            foreach (Vector3 point in trajectory)
                if (!IsFinite(point))
                    throw new InvalidOperationException($"候補の軌道に数値異常が発生しました。{context}");
            errorXY = default;
            if (!BallTrajectoryPredictor.TryGetCrossPointAtZ(trajectory, targetPoint.z, out Vector3 crossPoint))
                return false;

            if (!IsFinite(crossPoint))
                throw new InvalidOperationException($"目標平面の交点に数値異常が発生しました。{context}");
            errorXY = new Vector2(targetPoint.x - crossPoint.x, targetPoint.y - crossPoint.y);
            if (!IsFinite(errorXY) || !IsFinite(errorXY.sqrMagnitude))
                throw new InvalidOperationException($"目標平面上の誤差に数値異常が発生しました。{context}");
            return true;
        }

        if (!TryEvaluate(directionOffset, 0, "初期候補", out Vector3 bestVelocityMps, out Vector2 currentError))
            throw new InvalidOperationException($"初期候補が目標Z平面に到達しませんでした。Z={targetPoint.z:F3}, 反復=0");

        for (int i = 0; i < BallPhysicsConstants.MAX_OPTIMIZATION_ITERATIONS; i++)
        {
            if (currentError.magnitude < BallPhysicsConstants.POSITION_TOLERANCE)
                return new PitchSolveResult(bestVelocityMps, true, currentError.magnitude, i + 1);

            // 左右・上下へ少し向きを変え、平面上の誤差がどう変わるかを測る。
            if (!TryEvaluate(directionOffset + Vector2.right * DIRECTION_PROBE_STEP, i + 1, "左右方向のprobe", out _, out Vector2 rightError) ||
                !TryEvaluate(directionOffset + Vector2.up * DIRECTION_PROBE_STEP, i + 1, "上下方向のprobe", out _, out Vector2 upError))
            {
                throw new InvalidOperationException($"方向補正用のprobeが目標Z平面に到達しませんでした。Z={targetPoint.z:F3}, 反復={i + 1}");
            }

            Vector2 rightDerivative = (rightError - currentError) / DIRECTION_PROBE_STEP;
            Vector2 upDerivative = (upError - currentError) / DIRECTION_PROBE_STEP;
            if (!IsFinite(rightDerivative) || !IsFinite(upDerivative))
                throw new InvalidOperationException($"方向微分に数値異常が発生しました。反復={i + 1}");
            float determinant = rightDerivative.x * upDerivative.y - upDerivative.x * rightDerivative.y;
            if (float.IsNaN(determinant) || float.IsInfinity(determinant) || Mathf.Abs(determinant) < MIN_JACOBIAN_DETERMINANT)
            {
                throw new InvalidOperationException($"Jacobianを解けません。反復={i + 1}, determinant={determinant}, XY誤差={currentError.magnitude}m");
            }

            Vector2 directionStep = new Vector2(
                (upDerivative.x * currentError.y - upDerivative.y * currentError.x) / determinant,
                (rightDerivative.y * currentError.x - rightDerivative.x * currentError.y) / determinant);
            if (!IsFinite(directionStep) || !IsFinite(directionStep.sqrMagnitude))
                throw new InvalidOperationException($"投球方向の補正に数値異常が発生しました。反復={i + 1}");
            directionStep = Vector2.ClampMagnitude(directionStep, MAX_DIRECTION_STEP);

            bool improved = false;
            for (int backtrack = 0; backtrack < MAX_BACKTRACK_STEPS; backtrack++)
            {
                Vector2 candidateOffset = directionOffset + directionStep;
                if (TryEvaluate(candidateOffset, i + 1, $"backtracking候補{backtrack + 1}", out Vector3 candidateVelocityMps, out Vector2 candidateError) &&
                    candidateError.sqrMagnitude < currentError.sqrMagnitude)
                {
                    directionOffset = candidateOffset;
                    bestVelocityMps = candidateVelocityMps;
                    currentError = candidateError;
                    improved = true;
                    break;
                }
                // 補正が大きすぎる場合は、同じ方向で幅を半分にして試す。
                directionStep *= 0.5f;
            }

            if (!improved)
            {
                throw new InvalidOperationException($"backtrackingで改善候補を得られませんでした。Z={targetPoint.z:F3}, 反復={i + 1}, XY誤差={currentError.magnitude}m");
            }
        }

        return new PitchSolveResult(bestVelocityMps,
            currentError.magnitude < BallPhysicsConstants.POSITION_TOLERANCE,
            currentError.magnitude, BallPhysicsConstants.MAX_OPTIMIZATION_ITERATIONS);
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static bool IsFinite(Vector2 value) => IsFinite(value.x) && IsFinite(value.y);
    private static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
}
