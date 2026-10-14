using UnityEngine;

internal static class BallAerodynamics
{
    internal static Vector3 CalculateMagnusForce(Vector3 velocityMps, Vector3 spinAxis,
        float spinRateRpm, BattingParameters parameters)
    {
        Vector3 transverseSpin = Vector3.ProjectOnPlane(spinAxis.normalized * spinRateRpm, velocityMps.normalized);
        float transverseSpinRateRpm = transverseSpin.magnitude;
        float cl = BattingPhysics.CalculateLiftCoefficient(transverseSpinRateRpm, velocityMps.magnitude, parameters);
        return CalculateMagnusForce(velocityMps, transverseSpin.normalized, transverseSpinRateRpm, cl);
    }

    internal static Vector3 CalculateMagnusForce(Vector3 velocityMps, SpinState spin)
    {
        Vector3 transverseAngularVelocity = spin.GetTransverseAngularVelocity(velocityMps);
        // 軸を速度へ追従させず、現在の進行方向に直交する成分を取り出す。
        float transverseSpinRateRpm = transverseAngularVelocity.magnitude / BallPhysicsConstants.RPM_TO_RAD_PER_SEC;
        float liftCoeff = CalculatePitchLiftCoefficient(velocityMps.magnitude, transverseSpinRateRpm);
        return CalculateMagnusForce(velocityMps, transverseAngularVelocity.normalized, transverseSpinRateRpm, liftCoeff);
    }

    internal static Vector3 CalculateMagnusForce(
        Vector3 velocityMps,
        Vector3 spinAxisNorm,
        float spinRateRpm,
        float liftCoeff)
    {
        if (velocityMps.sqrMagnitude < BallPhysicsConstants.MIN_VELOCITY_SQUARED)
            return Vector3.zero;

        // 呼び出し元の回転数はrpm。ここでは外積の向きを求めるために使う。
        Vector3 spinVectorRpm = spinAxisNorm * spinRateRpm;
        Vector3 magnusDirection = Vector3.Cross(spinVectorRpm, velocityMps);

        if (magnusDirection.sqrMagnitude < BallPhysicsConstants.MIN_MAGNUS_DIRECTION_SQUARED)
            return Vector3.zero;

        magnusDirection.Normalize();

        float speedMps = velocityMps.magnitude;
        float magnusForceNewtons = BallPhysicsConstants.MAGNUS_FORCE_HALF
            * BallPhysicsConstants.AIR_DENSITY_KG_PER_M3
            * speedMps * speedMps
            * BallPhysicsConstants.CROSS_SECTION_M2
            * liftCoeff;

        return magnusDirection * magnusForceNewtons;
    }

    /// <summary>進行方向に直交する回転数（rpm）と球速（m/s）から揚力係数を求める。</summary>
    internal static float CalculatePitchLiftCoefficient(float speedMps, float transverseSpinRateRpm)
    {
        float transverseAngularSpeedRadPerSec = transverseSpinRateRpm * BallPhysicsConstants.RPM_TO_RAD_PER_SEC;
        float spinParam = speedMps > 0f
            ? (BallPhysicsConstants.BALL_RADIUS_M * transverseAngularSpeedRadPerSec) / speedMps
            : 0f;
        return Mathf.Clamp(
            1.5f * spinParam / (1f + 2.0f * spinParam),
            0f, 0.6f);
    }

    internal static Vector3 CalculatePitchAcceleration(Vector3 velocityMps, SpinState spin)
    {
        return Physics.gravity + (CalculateDragForce(velocityMps) + CalculateMagnusForce(velocityMps, spin))
            / BallPhysicsConstants.BALL_MASS_KG;
    }

    internal static Vector3 CalculateDragForce(Vector3 velocityMps)
    {
        float speedMps = velocityMps.magnitude;
        if (speedMps < BallPhysicsConstants.MIN_DRAG_VELOCITY)
            return Vector3.zero;

        float dragForceNewtons = BallPhysicsConstants.DRAG_FORCE_HALF
            * BallPhysicsConstants.AIR_DENSITY_KG_PER_M3
            * speedMps * speedMps
            * BallPhysicsConstants.CROSS_SECTION_M2
            * BallPhysicsConstants.DRAG_COEFFICIENT;

        return -velocityMps.normalized * dragForceNewtons;
    }
}
