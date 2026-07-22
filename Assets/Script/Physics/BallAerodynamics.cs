using UnityEngine;

internal static class BallAerodynamics
{
    internal static Vector3 CalculateMagnusForce(
        Vector3 velocity,
        Vector3 spinAxisNorm,
        float angularVelocity,
        float liftCoeff)
    {
        if (velocity.sqrMagnitude < BallPhysicsConstants.MIN_VELOCITY_SQUARED)
            return Vector3.zero;

        Vector3 spinVector = spinAxisNorm * angularVelocity;
        Vector3 magnusDirection = Vector3.Cross(spinVector, velocity);

        if (magnusDirection.sqrMagnitude < BallPhysicsConstants.MIN_MAGNUS_DIRECTION_SQUARED)
            return Vector3.zero;

        magnusDirection.Normalize();

        float velocityMagnitude = velocity.magnitude;
        float magnusForceMagnitude = BallPhysicsConstants.MAGNUS_FORCE_HALF
            * BallPhysicsConstants.AIR_DENSITY
            * velocityMagnitude * velocityMagnitude
            * BallPhysicsConstants.CROSS_SECTION
            * liftCoeff;

        return magnusDirection * magnusForceMagnitude;
    }

    internal static Vector3 CalculateDragForce(Vector3 velocity)
    {
        float velocityMagnitude = velocity.magnitude;
        if (velocityMagnitude < BallPhysicsConstants.MIN_DRAG_VELOCITY)
            return Vector3.zero;

        float dragMagnitude = BallPhysicsConstants.DRAG_FORCE_HALF
            * BallPhysicsConstants.AIR_DENSITY
            * velocityMagnitude * velocityMagnitude
            * BallPhysicsConstants.CROSS_SECTION
            * BallPhysicsConstants.DRAG_COEFFICIENT;

        return -velocity.normalized * dragMagnitude;
    }
}