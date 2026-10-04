using System;
using UnityEngine;

internal static class PitchVelocitySolver
{
    private const float DIRECTION_PROBE_STEP = 0.001f;
    private const float MAX_DIRECTION_STEP = 0.25f;
    private const float MIN_JACOBIAN_DETERMINANT = 0.000001f;
    private const int MAX_BACKTRACK_STEPS = 6;

    /// <summary>
    /// 設定球速を保ち、目標Z平面での左右・高さのずれが小さくなる投球方向を探す。
    /// </summary>
    internal static Vector3 FindOptimalVelocityAdvanced(
        Vector3 startPoint,
        Vector3 targetPoint,
        BallData ballData,
        float targetSpeedMps,
        TrajectorySettings settings,
        BounceSettings bounceSettings)
    {
        if (float.IsNaN(targetSpeedMps) || float.IsInfinity(targetSpeedMps) || targetSpeedMps <= 0f)
            throw new ArgumentOutOfRangeException(nameof(targetSpeedMps));

        Vector3 displacement = targetPoint - startPoint;
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
        float forwardComponent = Vector3.Dot(initialAim, forward);
        Vector2 directionOffset = forwardComponent > 1e-6f
            ? new Vector2(Vector3.Dot(initialAim, right), Vector3.Dot(initialAim, up)) / forwardComponent
            : Vector2.zero;

        bool TryEvaluate(Vector2 offset, out Vector3 candidateVelocityMps, out Vector2 errorXY)
        {
            Vector3 candidateDirection = forward + right * offset.x + up * offset.y;
            candidateVelocityMps = candidateDirection.normalized * targetSpeedMps;
            SpinState candidateSpin = SpinState.Create(candidateVelocityMps,
                ballData.RotateSpeed, ballData.SpinTilt, ballData.SpinEfficiency);
            var config = new BallPhysicsCalculator.SimulationConfig
            {
                DeltaTime = settings?.DeltaTime ?? 0.01f,
                MaxSimulationTimeSeconds = settings?.MaxSimulationTime ?? 5f,
                StopAtZ = settings?.StopAtTarget == true ? settings.StopPosition.z : (float?)null,
                BounceSettings = bounceSettings,
                PitchSpin = candidateSpin
            };
            var trajectory = BallTrajectorySimulator.SimulateTrajectory(
                startPoint, candidateVelocityMps, Vector3.zero, 0f, 0f, config);
            errorXY = default;
            if (!BallTrajectoryPredictor.TryGetCrossPointAtZ(trajectory, targetPoint.z, out Vector3 crossPoint))
                return false;

            errorXY = new Vector2(targetPoint.x - crossPoint.x, targetPoint.y - crossPoint.y);
            return !float.IsNaN(errorXY.sqrMagnitude) && !float.IsInfinity(errorXY.sqrMagnitude);
        }

        if (!TryEvaluate(directionOffset, out Vector3 bestVelocityMps, out Vector2 currentError))
        {
            Debug.LogWarning($"[最適化] 初期候補を目標Z平面で評価できませんでした。Z={targetPoint.z:F3}");
            return bestVelocityMps;
        }

        for (int i = 0; i < BallPhysicsConstants.MAX_OPTIMIZATION_ITERATIONS; i++)
        {
            if (currentError.magnitude < BallPhysicsConstants.POSITION_TOLERANCE)
                return bestVelocityMps;

            // 左右・上下へ少し向きを変え、平面上の誤差がどう変わるかを測る。
            if (!TryEvaluate(directionOffset + Vector2.right * DIRECTION_PROBE_STEP, out _, out Vector2 rightError) ||
                !TryEvaluate(directionOffset + Vector2.up * DIRECTION_PROBE_STEP, out _, out Vector2 upError))
            {
                Debug.LogWarning("[最適化] 方向補正用の候補を目標Z平面で評価できませんでした。");
                return bestVelocityMps;
            }

            Vector2 rightDerivative = (rightError - currentError) / DIRECTION_PROBE_STEP;
            Vector2 upDerivative = (upError - currentError) / DIRECTION_PROBE_STEP;
            float determinant = rightDerivative.x * upDerivative.y - upDerivative.x * rightDerivative.y;
            if (float.IsNaN(determinant) || float.IsInfinity(determinant) || Mathf.Abs(determinant) < MIN_JACOBIAN_DETERMINANT)
            {
                Debug.LogWarning("[最適化] 目標平面上の誤差から投球方向の補正量を求められませんでした。");
                return bestVelocityMps;
            }

            Vector2 directionStep = new Vector2(
                (upDerivative.x * currentError.y - upDerivative.y * currentError.x) / determinant,
                (rightDerivative.y * currentError.x - rightDerivative.x * currentError.y) / determinant);
            directionStep = Vector2.ClampMagnitude(directionStep, MAX_DIRECTION_STEP);

            bool improved = false;
            for (int backtrack = 0; backtrack < MAX_BACKTRACK_STEPS; backtrack++)
            {
                Vector2 candidateOffset = directionOffset + directionStep;
                if (TryEvaluate(candidateOffset, out Vector3 candidateVelocityMps, out Vector2 candidateError) &&
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
                Debug.LogWarning("[最適化] 投球方向を補正しても誤差が改善しないため、探索を終了しました。");
                return bestVelocityMps;
            }
        }

        if (currentError.magnitude >= BallPhysicsConstants.POSITION_TOLERANCE)
            Debug.LogWarning($"[最適化] 探索上限に達しました。XY誤差={currentError.magnitude:F4}m");
        return bestVelocityMps;
    }
}
