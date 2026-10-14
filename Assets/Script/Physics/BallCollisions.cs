using UnityEngine;

public struct GroundHitResult
{
    public bool DidHitGround;
    public Vector3 HitPoint;
    public string LayerName;
    public bool ShouldRoll;
}


internal static class BallCollisions
{
    // 既存アセットの減速率は、0.01秒ごとの速度の残り割合として扱う。
    private const float ROLLING_REFERENCE_SECONDS = 0.01f;
    public static bool TryReflectOnFence(
    Vector3 from,
    ref Vector3 to,
    ref Vector3 velocity,
    float deltaTime,
    float restitution)
    {
        int fenceMask = LayerMask.GetMask("Net");

        Vector3 dir = to - from;
        float dist = dir.magnitude;
        if (dist <= 1e-6f) return false;

        dir /= dist;

        float radius = BaseballWorldScale.ToWorldUnits(BallPhysicsConstants.BALL_RADIUS_M);

        if (Physics.SphereCast(from, radius, dir, out RaycastHit hit, dist, fenceMask, QueryTriggerInteraction.Ignore))
        {
            // 接触点の少し手前に戻す（めり込み防止）
            float backOff = BaseballWorldScale.ToWorldUnits(BallPhysicsConstants.NET_EPSILON);
            to = hit.point + hit.normal * (radius + backOff);

            // 速度反射：v' = v - 2*(v·n)*n
            Vector3 n = hit.normal.normalized;
            float vn = Vector3.Dot(velocity, n);

            if (vn < 0f)
            {
                Vector3 reflected = velocity - 2f * vn * n;
                velocity = reflected * Mathf.Clamp01(restitution);
            }

            return true;
        }

        return false;
    }


    public static bool HandleGroundBounce(ref Vector3 velocity, BounceSettings settings, int bounceCount)
    {
        velocity.y = -velocity.y * settings.groundRestitution;

        float horizontalSpeed = new Vector2(velocity.x, velocity.z).magnitude;
        float frictionLoss = horizontalSpeed * settings.groundFriction;

        if (horizontalSpeed > 0.01f)
        {
            Vector3 horizontalDir = new Vector3(velocity.x, 0, velocity.z).normalized;
            float newHorizontalSpeed = Mathf.Max(0, horizontalSpeed - frictionLoss);
            velocity.x = horizontalDir.x * newHorizontalSpeed;
            velocity.z = horizontalDir.z * newHorizontalSpeed;
        }

        bool shouldRoll = velocity.y < 2f && bounceCount >= settings.maxBounces;
        return shouldRoll;
    }

    public static void HandleWallBounce(ref Vector3 position, ref Vector3 velocity, BounceSettings settings)
    {
        Bounds bounds = settings.fieldBounds;

        if (position.x < bounds.min.x)
        {
            position.x = bounds.min.x;
            velocity.x = -velocity.x * settings.wallRestitution;
        }
        else if (position.x > bounds.max.x)
        {
            position.x = bounds.max.x;
            velocity.x = -velocity.x * settings.wallRestitution;
        }

        if (position.z < bounds.min.z)
        {
            position.z = bounds.min.z;
            velocity.z = -velocity.z * settings.wallRestitution;
        }
        else if (position.z > bounds.max.z)
        {
            position.z = bounds.max.z;
            velocity.z = -velocity.z * settings.wallRestitution;
        }
    }

    public static Vector3 SimulateRolling(Vector3 position, ref Vector3 velocity,
                                         BounceSettings settings, float deltaTime)
    {
        velocity.y = 0f;
        float retention = Mathf.Clamp01(settings.rollingDeceleration);
        float moveSeconds;
        float decay;
        if (retention <= 0f)
        {
            moveSeconds = 0f;
            decay = 0f;
        }
        else if (retention >= 1f)
        {
            moveSeconds = deltaTime;
            decay = 1f;
        }
        else
        {
            float decayRate = Mathf.Log(retention) / ROLLING_REFERENCE_SECONDS;
            decay = Mathf.Exp(decayRate * deltaTime);
            // 減速中の移動距離も積分し、時間刻みで転がる距離が変わらないようにする。
            moveSeconds = (decay - 1f) / decayRate;
        }
        Vector3 nextPosition = position + BaseballWorldScale.ToWorldUnits(velocity * moveSeconds);
        velocity *= decay;
        nextPosition.y = settings.groundLevel;
        return nextPosition;
    }

    public static bool IsOutOfBounds(Vector3 position, Bounds bounds)
    {
        return position.x < bounds.min.x || position.x > bounds.max.x ||
               position.z < bounds.min.z || position.z > bounds.max.z;
    }
}
