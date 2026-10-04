using UnityEngine;

internal static class BallAerodynamics
{
    internal static Vector3 CalculateMagnusForce(Vector3 velocityMps, SpinState spin)
    {
        Vector3 transverseAngularVelocity = spin.GetTransverseAngularVelocity(velocityMps);
        // 軸を速度へ追従させず、現在の進行方向に直交する成分を取り出す。
        float transverseSpinRateRpm = transverseAngularVelocity.magnitude / BallPhysicsConstants.RPM_TO_RAD_PER_SEC;
        float liftCoeff = BallPhysicsCalculator.CalcCl(velocityMps.magnitude, transverseSpinRateRpm);
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
            * BallPhysicsConstants.AIR_DENSITY
            * speedMps * speedMps
            * BallPhysicsConstants.CROSS_SECTION
            * liftCoeff;

        return magnusDirection * magnusForceNewtons;
    }

    internal static Vector3 CalculateDragForce(Vector3 velocityMps)
    {
        float speedMps = velocityMps.magnitude;
        if (speedMps < BallPhysicsConstants.MIN_DRAG_VELOCITY)
            return Vector3.zero;

        float dragForceNewtons = BallPhysicsConstants.DRAG_FORCE_HALF
            * BallPhysicsConstants.AIR_DENSITY
            * speedMps * speedMps
            * BallPhysicsConstants.CROSS_SECTION
            * BallPhysicsConstants.DRAG_COEFFICIENT;

        return -velocityMps.normalized * dragForceNewtons;
    }
}
