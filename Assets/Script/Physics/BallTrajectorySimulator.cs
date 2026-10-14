using System.Collections.Generic;
using UnityEngine;

public struct TrajectoryResult
{
    public List<Vector3> Points;
    public string FirstGroundLayer;
    /// <summary>地面の反射処理を実行した回数。</summary>
    public int BounceCount;
    public int LandingIndex;
}

public static class BallTrajectorySimulator
{
    /// <summary>Raycast開始位置のオフセット(m)</summary>
    private const float RAYCAST_START_OFFSET = 5f;

    /// <summary>Raycastの最大距離(m)</summary>
    private const float RAYCAST_MAX_DISTANCE = 20f;

    /// <summary>OverlapSphereの検索半径(m)</summary>
    private const float GROUND_DETECTION_RADIUS = 0.5f;

    // 接地直前の高さに生じる小さな丸め誤差を許容する（m）。
    private const float GROUND_CONTACT_EPSILON = 0.00001f;

    private const string LAYER_GROUND = "Ground";
    private const string LAYER_HOMERUN_ZONE = "HomerunZone";
    private const string LAYER_UNKNOWN = "Unknown";

    private static void SimulatePhysicsStep(
        ref Vector3 position,
        ref Vector3 velocityMps,
        Vector3 spinAxisNormalized,
        float spinRateRpm,
        float liftCoefficient,
        float DeltaTime,
        SpinState? pitchSpin,
        BattingParameters battingParameters)
    {
        if (pitchSpin.HasValue)
        {
            IntegratePitch(ref position, ref velocityMps, pitchSpin.Value, DeltaTime);
            return;
        }

        Vector3 gravityMetersPerSecondSquared = Physics.gravity;
        Vector3 dragForceNewtons = BallAerodynamics.CalculateDragForce(velocityMps);
        Vector3 magnusForceNewtons = battingParameters != null
            ? BallAerodynamics.CalculateMagnusForce(velocityMps, spinAxisNormalized, spinRateRpm, battingParameters)
            : BallAerodynamics.CalculateMagnusForce(
            velocityMps,
            spinAxisNormalized,
            spinRateRpm,
            liftCoefficient
        );

        Vector3 accelerationMetersPerSecondSquared = gravityMetersPerSecondSquared + (dragForceNewtons + magnusForceNewtons) / BallPhysicsConstants.BALL_MASS_KG;

        velocityMps += accelerationMetersPerSecondSquared * DeltaTime;
        position += BaseballWorldScale.ToWorldUnits(velocityMps * DeltaTime);
    }

    // 力は速度と固定した回転ベクトルから求める。位置と速度を同じRK4の段階で積分する。
    private static void IntegratePitch(ref Vector3 position, ref Vector3 velocityMps, SpinState spin, float dt)
    {
        Vector3 v1 = velocityMps;
        Vector3 a1 = BallAerodynamics.CalculatePitchAcceleration(v1, spin);
        Vector3 v2 = velocityMps + a1 * (dt * .5f);
        Vector3 a2 = BallAerodynamics.CalculatePitchAcceleration(v2, spin);
        Vector3 v3 = velocityMps + a2 * (dt * .5f);
        Vector3 a3 = BallAerodynamics.CalculatePitchAcceleration(v3, spin);
        Vector3 v4 = velocityMps + a3 * dt;
        Vector3 a4 = BallAerodynamics.CalculatePitchAcceleration(v4, spin);
        position += BaseballWorldScale.ToWorldUnits((v1 + 2f * v2 + 2f * v3 + v4) * (dt / 6f));
        velocityMps += (a1 + 2f * a2 + 2f * a3 + a4) * (dt / 6f);
    }

    // 接地情報が必要な場合だけ、最初の着地点を調べる。
    private static TrajectoryResult SimulateTrajectoryInternal(
        Vector3 startPosition,
        Vector3 initialVelocityMps,
        Vector3 spinAxisNormalized,
        float spinRateRpm,
        float liftCoefficient,
        BallPhysicsCalculator.SimulationConfig config,
        bool trackGroundLayer)
    {
        List<Vector3> trajectory = new List<Vector3> { startPosition };
        Vector3 position = startPosition;
        Vector3 velocityMps = initialVelocityMps;

        float elapsedSeconds = 0f;
        int bounceCount = 0;
        bool isRolling = false;
        bool hasGroundContact = config.BounceSettings != null &&
                                startPosition.y <= config.BounceSettings.groundLevel &&
                                initialVelocityMps.y >= 0f;
        string firstGroundLayer = null;
        int landingIndex = 0;

        while (elapsedSeconds < config.MaxSimulationTimeSeconds)
        {
            Vector3 prevPos = position;
            Vector3 newPos = position;
            Vector3 newVelocityMps = velocityMps;

            // 転がり中は飛翔計算を重ねず、水平移動を1回だけ進める。
            if (isRolling && config.BounceSettings != null)
                newPos = BallCollisions.SimulateRolling(newPos, ref newVelocityMps, config.BounceSettings, config.DeltaTime);
            else
                SimulatePhysicsStep(ref newPos, ref newVelocityMps, spinAxisNormalized,
                                   spinRateRpm, liftCoefficient, config.DeltaTime, config.PitchSpin, config.BattingParameters);

            if (!isRolling && config.BounceSettings != null && config.BounceSettings.enableWallBounce)
            {
                BallCollisions.TryReflectOnFence(prevPos, ref newPos, ref newVelocityMps,
                                                config.DeltaTime,
                                                config.BounceSettings.wallRestitution);
            }

            if (config.BounceSettings != null)
            {
                float groundLevel = config.BounceSettings.groundLevel;
                if (newPos.y > groundLevel)
                {
                    hasGroundContact = false;
                }
                else
                {
                    bool didHitGround = !isRolling && !hasGroundContact &&
                                        prevPos.y >= groundLevel - BaseballWorldScale.ToWorldUnits(GROUND_CONTACT_EPSILON) &&
                                        newPos.y < prevPos.y && newVelocityMps.y < 0f;
                    newPos.y = groundLevel;

                    if (didHitGround)
                    {
                        // 接地情報は最初の1回だけ記録し、反射は毎回の接地で行う。
                        if (trackGroundLayer && firstGroundLayer == null)
                        {
                            firstGroundLayer = DetectGroundLayer(newPos, config.BounceSettings);
                            landingIndex = trajectory.Count;
                        }

                        // 回数の基準は従来どおり、今回の接地より前に反射した回数。
                        bool shouldRoll = BallCollisions.HandleGroundBounce(
                            ref newVelocityMps, config.BounceSettings, bounceCount);
                        bounceCount++;
                        if (shouldRoll)
                        {
                            isRolling = true;
                            newVelocityMps.y = 0f;
                        }
                    }
                    else if (newVelocityMps.y < 0f)
                    {
                        // 離地していない間は反射を繰り返さず、地面への沈み込みだけを抑える。
                        newVelocityMps.y = 0f;
                    }

                    hasGroundContact = true;
                }
            }

            position = newPos;
            velocityMps = newVelocityMps;
            trajectory.Add(position);

            elapsedSeconds += config.DeltaTime;

            if (isRolling && config.BounceSettings != null)
            {
                if (velocityMps.magnitude < config.BounceSettings.stopVelocityThreshold)
                    break;
            }

            if (config.StopAtZ.HasValue)
            {
                float stopZ = config.StopAtZ.Value;
                float currZ = position.z;

                if ((prevPos.z - stopZ) * (currZ - stopZ) <= 0f)
                    break;
            }
        }

        return new TrajectoryResult
        {
            Points = trajectory,
            FirstGroundLayer = firstGroundLayer,
            BounceCount = bounceCount,
            LandingIndex = landingIndex
        };
    }

    /// <summary>接地レイヤーの検出を省き、軌道上の位置だけを返す。</summary>
    public static List<Vector3> SimulateTrajectory(
        Vector3 startPosition,
        Vector3 initialVelocityMps,
        Vector3 spinAxisNormalized,
        float spinRateRpm,
        float liftCoefficient,
        BallPhysicsCalculator.SimulationConfig config)
    {
        var result = SimulateTrajectoryInternal(
            startPosition, initialVelocityMps, spinAxisNormalized,
            spinRateRpm, liftCoefficient, config,
            trackGroundLayer: false);

        return result.Points;
    }

    public static TrajectoryResult SimulateTrajectoryWithMetadata(
        Vector3 startPosition,
        Vector3 initialVelocityMps,
        Vector3 spinAxisNormalized,
        float spinRateRpm,
        float liftCoefficient,
        BallPhysicsCalculator.SimulationConfig config)
    {
        return SimulateTrajectoryInternal(
            startPosition, initialVelocityMps, spinAxisNormalized,
            spinRateRpm, liftCoefficient, config,
            trackGroundLayer: true);
    }

    private static string DetectGroundLayer(Vector3 position, BounceSettings settings)
    {
        if (settings == null)
            return LAYER_UNKNOWN;

        Vector3 center = new Vector3(
            position.x,
            settings.groundLevel,
            position.z
        );

        int mask = LayerMask.GetMask(LAYER_GROUND, LAYER_HOMERUN_ZONE);
        if (mask == 0)
        {
            Debug.LogWarning($"[DetectGroundLayer] {LAYER_GROUND}/{LAYER_HOMERUN_ZONE} レイヤーが存在しません");
            mask = ~0; // すべてのレイヤーを対象
        }

        if (Physics.Raycast(
            center + Vector3.up * BaseballWorldScale.ToWorldUnits(RAYCAST_START_OFFSET),
            Vector3.down,
            out RaycastHit hit,
            BaseballWorldScale.ToWorldUnits(RAYCAST_MAX_DISTANCE),
            mask,
            QueryTriggerInteraction.Collide))
        {
            string layerName = LayerMask.LayerToName(hit.collider.gameObject.layer);
            return layerName;
        }

        // 地面に埋まった場合は下からも探す。
        if (Physics.Raycast(
            center + Vector3.down * BaseballWorldScale.ToWorldUnits(RAYCAST_START_OFFSET),
            Vector3.up,

            out hit,
            BaseballWorldScale.ToWorldUnits(RAYCAST_MAX_DISTANCE),
            mask,
            QueryTriggerInteraction.Collide))
        {
            string layerName = LayerMask.LayerToName(hit.collider.gameObject.layer);
            return layerName;
        }

        // 上下のRaycastで接地面が見つからなければ、周囲のコライダーを調べる。
        Collider[] colliders = Physics.OverlapSphere(
            center,
            BaseballWorldScale.ToWorldUnits(GROUND_DETECTION_RADIUS),
            mask,
            QueryTriggerInteraction.Collide);

        if (colliders != null && colliders.Length > 0)
        {
            Collider nearest = colliders[0];
            float bestDistanceSqr = (nearest.ClosestPoint(center) - center).sqrMagnitude;

            for (int i = 1; i < colliders.Length; i++)
            {
                Vector3 closestPoint = colliders[i].ClosestPoint(center);
                float distanceSqr = (closestPoint - center).sqrMagnitude;

                if (distanceSqr < bestDistanceSqr)
                {
                    bestDistanceSqr = distanceSqr;
                    nearest = colliders[i];
                }
            }

            string layerName = LayerMask.LayerToName(nearest.gameObject.layer);
            return layerName;
        }

        return LAYER_UNKNOWN;
    }
}
