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
        Vector3 spinAxisNormalized,
        float spinRateRpm,
        float liftCoefficient,
        float targetSpeedMps,
        TrajectorySettings settings,
        BounceSettings bounceSettings)
    {
        Debug.Log("[最適化] 開始");

        Vector3 currentVelocityMps = EstimateInitialVelocityImproved(
            startPoint,
            targetPoint,
            spinAxisNormalized,
            spinRateRpm,
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
                BounceSettings = bounceSettings
            };

            List<Vector3> testTrajectory = BallTrajectorySimulator.SimulateTrajectory(
                startPoint,
                currentVelocityMps,
                spinAxisNormalized,
                spinRateRpm,
                liftCoefficient,
                config
            );

            if (testTrajectory.Count == 0)
            {
                Debug.LogWarning("[最適化] 軌道計算失敗");
                break;
            }

            Vector3 endPoint = testTrajectory[testTrajectory.Count - 1];
            Vector3 error = targetPoint - endPoint;

            float errorZ = Mathf.Abs(error.z);
            float errorXY = new Vector2(error.x, error.y).magnitude;
            float totalError = errorZ * BallPhysicsConstants.Z_ERROR_WEIGHT + errorXY;

            if (totalError < bestError)
            {
                bestError = totalError;
                bestVelocityMps = currentVelocityMps;
            }

            if (errorZ < BallPhysicsConstants.POSITION_TOLERANCE * BallPhysicsConstants.Z_TOLERANCE_FACTOR &&
                errorXY < BallPhysicsConstants.POSITION_TOLERANCE)
            {
                return currentVelocityMps;
            }

            float progress = (float)i / BallPhysicsConstants.MAX_OPTIMIZATION_ITERATIONS;

            if (errorZ > BallPhysicsConstants.Z_POSITION_TOLERANCE)
            {
                float zAdjustment = error.z * Mathf.Lerp(BallPhysicsConstants.Z_ADJUSTMENT_INITIAL, BallPhysicsConstants.Z_ADJUSTMENT_FINAL, progress);
                currentVelocityMps.z += zAdjustment;
            }

            Vector3 xyAdjustment = new Vector3(error.x, error.y, 0) *
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
        Vector3 spinAxisNormalized,
        float spinRateRpm,
        float liftCoefficient,
        float targetSpeedMps)
    {
        Vector3 displacement = targetPoint - startPoint;
        float horizontalDistanceMeters = new Vector2(displacement.x, displacement.z).magnitude;
        float verticalDistanceMeters = displacement.y;

        float dragFactor = BallPhysicsConstants.DRAG_FACTOR_BASE +
                          (BallPhysicsConstants.DRAG_COEFFICIENT * BallPhysicsConstants.AIR_DENSITY * BallPhysicsConstants.CROSS_SECTION * targetSpeedMps) /
                          (BallPhysicsConstants.DRAG_MASS_FACTOR * BallPhysicsConstants.BALL_MASS);

        float estimatedTimeSeconds = (horizontalDistanceMeters / targetSpeedMps) * dragFactor;

        float gravityMetersPerSecondSquared = Mathf.Abs(Physics.gravity.y);
        float gravityDropMeters = BallPhysicsConstants.GRAVITY_HALF * gravityMetersPerSecondSquared * estimatedTimeSeconds * estimatedTimeSeconds;

        float angularVelocityRadPerSec = spinRateRpm * BallPhysicsConstants.RPM_TO_RAD_PER_SEC;
        Vector3 forwardDir = displacement.normalized;
        Vector3 angularVelocityVectorRadPerSec = spinAxisNormalized * angularVelocityRadPerSec;
        Vector3 magnusDir = Vector3.Cross(angularVelocityVectorRadPerSec, forwardDir).normalized;

        float magnusAccelerationMetersPerSecondSquared = BallPhysicsConstants.MAGNUS_FORCE_HALF * BallPhysicsConstants.AIR_DENSITY * targetSpeedMps * targetSpeedMps
                           * BallPhysicsConstants.CROSS_SECTION * liftCoefficient / BallPhysicsConstants.BALL_MASS;
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
