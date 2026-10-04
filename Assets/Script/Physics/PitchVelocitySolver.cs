using System.Collections.Generic;
using UnityEngine;

internal static class PitchVelocitySolver
{
    /// <summary>
    /// 終点に到達する最適な初速を探索
    /// </summary>
    internal static Vector3 FindOptimalVelocityAdvanced(
        Vector3 startPoint,
        Vector3 targetPoint,
        BallData ballData,
        float targetSpeedMps,
        TrajectorySettings settings,
        BounceSettings bounceSettings)
    {
        Debug.Log("[最適化] 開始");

        Vector3 estimatedVelocityMps = (targetPoint - startPoint).normalized * targetSpeedMps;
        SpinState estimateSpin = SpinState.Create(estimatedVelocityMps, ballData.RotateSpeed, ballData.SpinTilt, ballData.SpinEfficiency);
        Vector3 transverseAxisNormalized = estimateSpin.GetTransverseAngularVelocity(estimatedVelocityMps).normalized;
        float transverseSpinRateRpm = ballData.RotateSpeed * ballData.SpinEfficiency;
        float liftCoefficient = BallPhysicsCalculator.CalcCl(targetSpeedMps, transverseSpinRateRpm);

        Vector3 currentVelocityMps = EstimateInitialVelocityImproved(
            startPoint,
            targetPoint,
            transverseAxisNormalized,
            transverseSpinRateRpm,
            liftCoefficient,
            targetSpeedMps
        );

        Vector3 bestVelocityMps = currentVelocityMps;
        float bestError = float.MaxValue;

        for (int i = 0; i < BallPhysicsConstants.MAX_OPTIMIZATION_ITERATIONS; i++)
        {
            var config = new BallPhysicsCalculator.SimulationConfig
            {
                DeltaTime = settings?.DeltaTime ?? 0.01f,
                MaxSimulationTimeSeconds = settings?.MaxSimulationTime ?? 5f,
                StopAtZ = settings?.StopAtTarget == true ? settings.StopPosition.z : (float?)null,
                BounceSettings = bounceSettings,
                PitchSpin = SpinState.Create(currentVelocityMps, ballData.RotateSpeed, ballData.SpinTilt, ballData.SpinEfficiency)
            };

            List<Vector3> testTrajectory = BallTrajectorySimulator.SimulateTrajectory(
                startPoint,
                currentVelocityMps,
                transverseAxisNormalized,
                transverseSpinRateRpm,
                liftCoefficient,
                config
            );

            if (testTrajectory.Count == 0)
            {
                Debug.LogWarning("[最適化] 軌道計算失敗");
                break;
            }

            if (!BallTrajectoryPredictor.TryGetCrossPointAtZ(testTrajectory, targetPoint.z, out Vector3 crossPoint))
            {
                Debug.LogWarning($"[最適化] 目標Z平面に到達しませんでした。Z={targetPoint.z:F3}, 試行={i + 1}");
                break;
            }

            // 目標平面を通過した位置で左右・高さのずれを評価する。
            Vector2 errorXY = new Vector2(targetPoint.x - crossPoint.x, targetPoint.y - crossPoint.y);
            float totalError = errorXY.magnitude;

            if (totalError < bestError)
            {
                bestError = totalError;
                bestVelocityMps = currentVelocityMps;
            }

            if (totalError < BallPhysicsConstants.POSITION_TOLERANCE)
            {
                return currentVelocityMps;
            }

            float progress = (float)i / BallPhysicsConstants.MAX_OPTIMIZATION_ITERATIONS;

            Vector3 xyAdjustment = new Vector3(errorXY.x, errorXY.y, 0f) *
                                   Mathf.Lerp(BallPhysicsConstants.XY_ADJUSTMENT_INITIAL, BallPhysicsConstants.XY_ADJUSTMENT_FINAL, progress);
            currentVelocityMps += xyAdjustment;

            float currentSpeedMps = currentVelocityMps.magnitude;
            float speedErrorMps = targetSpeedMps - currentSpeedMps;

            if (Mathf.Abs(speedErrorMps) > targetSpeedMps * BallPhysicsConstants.SPEED_ERROR_THRESHOLD)
            {
                float speedAdjustmentFactor = Mathf.Lerp(BallPhysicsConstants.SPEED_ADJUSTMENT_INITIAL, BallPhysicsConstants.SPEED_ADJUSTMENT_FINAL, progress);
                currentVelocityMps = currentVelocityMps.normalized *
                                  Mathf.Lerp(currentSpeedMps, targetSpeedMps, speedAdjustmentFactor);
            }
        }

        return bestVelocityMps;
    }

    /// <summary>
    /// マグヌス効果、空気抵抗を考慮して、目標に到達する初速を推定
    /// </summary>
    private static Vector3 EstimateInitialVelocityImproved(
        Vector3 startPoint,
        Vector3 targetPoint,
        Vector3 transverseAxisNormalized,
        float transverseSpinRateRpm,
        float liftCoefficient,
        float targetSpeedMps)
    {
        Vector3 displacement = targetPoint - startPoint;
        float horizontalDistanceMeters = new Vector2(displacement.x, displacement.z).magnitude;
        float verticalDistanceMeters = displacement.y;

        float dragFactor = BallPhysicsConstants.DRAG_FACTOR_BASE +
                          (BallPhysicsConstants.DRAG_COEFFICIENT * BallPhysicsConstants.AIR_DENSITY_KG_PER_M3 * BallPhysicsConstants.CROSS_SECTION_M2 * targetSpeedMps) /
                          (BallPhysicsConstants.DRAG_MASS_FACTOR * BallPhysicsConstants.BALL_MASS_KG);

        float estimatedTimeSeconds = (horizontalDistanceMeters / targetSpeedMps) * dragFactor;

        float gravityMetersPerSecondSquared = Mathf.Abs(Physics.gravity.y);
        float gravityDropMeters = BallPhysicsConstants.GRAVITY_HALF * gravityMetersPerSecondSquared * estimatedTimeSeconds * estimatedTimeSeconds;

        float angularSpeedRadPerSec = transverseSpinRateRpm * BallPhysicsConstants.RPM_TO_RAD_PER_SEC;
        Vector3 forwardDir = displacement.normalized;
        Vector3 angularVelocityVectorRadPerSec = transverseAxisNormalized * angularSpeedRadPerSec;
        Vector3 magnusDir = Vector3.Cross(angularVelocityVectorRadPerSec, forwardDir).normalized;

        float magnusAccelerationMetersPerSecondSquared = BallPhysicsConstants.MAGNUS_FORCE_HALF * BallPhysicsConstants.AIR_DENSITY_KG_PER_M3 * targetSpeedMps * targetSpeedMps
                           * BallPhysicsConstants.CROSS_SECTION_M2 * liftCoefficient / BallPhysicsConstants.BALL_MASS_KG;
        float magnusDisplacementMeters = BallPhysicsConstants.GRAVITY_HALF * magnusAccelerationMetersPerSecondSquared * estimatedTimeSeconds * estimatedTimeSeconds;

        float zSpeedMps = displacement.z / estimatedTimeSeconds;
        float xSpeedMps = displacement.x / estimatedTimeSeconds;
        float verticalSpeedMps = verticalDistanceMeters / estimatedTimeSeconds + gravityMetersPerSecondSquared * estimatedTimeSeconds * BallPhysicsConstants.GRAVITY_HALF;

        float magnusVerticalEffectMps = magnusDir.y * magnusDisplacementMeters / estimatedTimeSeconds;
        verticalSpeedMps -= magnusVerticalEffectMps * BallPhysicsConstants.MAGNUS_VERTICAL_CORRECTION_FACTOR;

        Vector3 initialVelocityMps = new Vector3(xSpeedMps, verticalSpeedMps, zSpeedMps);

        return initialVelocityMps;
    }
}
