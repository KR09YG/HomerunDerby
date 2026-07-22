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
        float spinRateRPM,
        float liftCoefficient,
        float desiredSpeed,
        TrajectorySettings settings,
        BounceSettings bounceSettings)
    {
        Debug.Log("[最適化] 開始");

        Vector3 currentVelocity = EstimateInitialVelocityImproved(
            startPoint,
            targetPoint,
            spinAxisNormalized,
            spinRateRPM,
            liftCoefficient,
            desiredSpeed
        );

        Vector3 bestVelocity = currentVelocity;
        float bestError = float.MaxValue;

        for (int i = 0; i < BallPhysicsConstants.MAX_OPTIMIZATION_ITERATIONS; i++)
        {
            var config = new BallPhysicsCalculator.SimulationConfig
            {
                DeltaTime = settings?.DeltaTime ?? 0.01f,
                MaxSimulationTime = settings?.MaxSimulationTime ?? 5f,
                StopAtZ = settings?.StopAtTarget == true ? settings.StopPosition.z : (float?)null,
                BounceSettings = bounceSettings
            };

            List<Vector3> testTrajectory = BallTrajectorySimulator.SimulateTrajectory(
                startPoint,
                currentVelocity,
                spinAxisNormalized,
                spinRateRPM,
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
                bestVelocity = currentVelocity;
            }

            if (errorZ < BallPhysicsConstants.POSITION_TOLERANCE * BallPhysicsConstants.Z_TOLERANCE_FACTOR &&
                errorXY < BallPhysicsConstants.POSITION_TOLERANCE)
            {
                return currentVelocity;
            }

            float progress = (float)i / BallPhysicsConstants.MAX_OPTIMIZATION_ITERATIONS;

            if (errorZ > BallPhysicsConstants.Z_POSITION_TOLERANCE)
            {
                float zAdjustment = error.z * Mathf.Lerp(BallPhysicsConstants.Z_ADJUSTMENT_INITIAL, BallPhysicsConstants.Z_ADJUSTMENT_FINAL, progress);
                currentVelocity.z += zAdjustment;
            }

            Vector3 xyAdjustment = new Vector3(error.x, error.y, 0) *
                                   Mathf.Lerp(BallPhysicsConstants.XY_ADJUSTMENT_INITIAL, BallPhysicsConstants.XY_ADJUSTMENT_FINAL, progress);
            currentVelocity += xyAdjustment;

            float currentSpeed = currentVelocity.magnitude;
            float speedError = desiredSpeed - currentSpeed;

            if (Mathf.Abs(speedError) > desiredSpeed * BallPhysicsConstants.SPEED_ERROR_THRESHOLD)
            {
                float speedAdjustmentFactor = Mathf.Lerp(BallPhysicsConstants.SPEED_ADJUSTMENT_INITIAL, BallPhysicsConstants.SPEED_ADJUSTMENT_FINAL, progress);
                currentVelocity = currentVelocity.normalized *
                                  Mathf.Lerp(currentSpeed, desiredSpeed, speedAdjustmentFactor);
            }
        }

        return bestVelocity;
    }

    /// <summary>
    /// マグヌス効果、空気抵抗を考慮して、目標に到達する初速を推定
    /// </summary>
    private static Vector3 EstimateInitialVelocityImproved(
        Vector3 startPoint,
        Vector3 targetPoint,
        Vector3 spinAxisNormalized,
        float spinRateRPM,
        float liftCoefficient,
        float desiredSpeed)
    {
        Vector3 displacement = targetPoint - startPoint;
        float horizontalDist = new Vector2(displacement.x, displacement.z).magnitude;
        float verticalDist = displacement.y;

        float dragFactor = BallPhysicsConstants.DRAG_FACTOR_BASE +
                          (BallPhysicsConstants.DRAG_COEFFICIENT * BallPhysicsConstants.AIR_DENSITY * BallPhysicsConstants.CROSS_SECTION * desiredSpeed) /
                          (BallPhysicsConstants.DRAG_MASS_FACTOR * BallPhysicsConstants.BALL_MASS);

        float estimatedTime = (horizontalDist / desiredSpeed) * dragFactor;

        float gravity = Mathf.Abs(Physics.gravity.y);
        float gravityDrop = BallPhysicsConstants.GRAVITY_HALF * gravity * estimatedTime * estimatedTime;

        float angularVelocity = spinRateRPM * BallPhysicsConstants.RPM_TO_RAD_PER_SEC;
        Vector3 forwardDir = displacement.normalized;
        Vector3 spinVector = spinAxisNormalized * angularVelocity;
        Vector3 magnusDir = Vector3.Cross(spinVector, forwardDir).normalized;

        float magnusAccel = BallPhysicsConstants.MAGNUS_FORCE_HALF * BallPhysicsConstants.AIR_DENSITY * desiredSpeed * desiredSpeed
                           * BallPhysicsConstants.CROSS_SECTION * liftCoefficient / BallPhysicsConstants.BALL_MASS;
        float magnusDisplacement = BallPhysicsConstants.GRAVITY_HALF * magnusAccel * estimatedTime * estimatedTime;

        float zSpeed = displacement.z / estimatedTime;
        float xSpeed = displacement.x / estimatedTime;
        float verticalSpeed = verticalDist / estimatedTime + gravity * estimatedTime * BallPhysicsConstants.GRAVITY_HALF;

        float magnusVerticalEffect = magnusDir.y * magnusDisplacement / estimatedTime;
        verticalSpeed -= magnusVerticalEffect * BallPhysicsConstants.MAGNUS_VERTICAL_CORRECTION_FACTOR;

        Vector3 initialVelocity = new Vector3(xSpeed, verticalSpeed, zSpeed);

        return initialVelocity;
    }
}
