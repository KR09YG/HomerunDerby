using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 軌道シミュレーション結果
/// </summary>
public struct TrajectoryResult
{
    public List<Vector3> Points;
    public string FirstGroundLayer;
    public int BounceCount;
    public int LandingIndex;
}

public static class BallTrajectorySimulator
{
    // ===== 物理定数 =====
    /// <summary>野球ボールの質量(kg)</summary>
    private const float BALL_MASS_KG = 0.145f;

    // ===== レイヤー検出用定数 =====
    /// <summary>Raycast開始位置のオフセット(m)</summary>
    private const float RAYCAST_START_OFFSET = 5f;

    /// <summary>Raycastの最大距離(m)</summary>
    private const float RAYCAST_MAX_DISTANCE = 20f;

    /// <summary>OverlapSphereの検索半径(m)</summary>
    private const float GROUND_DETECTION_RADIUS = 0.5f;

    // ===== レイヤー名定数 =====
    private const string LAYER_GROUND = "Ground";
    private const string LAYER_HOMERUN_ZONE = "HomerunZone";
    private const string LAYER_UNKNOWN = "Unknown";

    /// <summary>
    /// 物理ステップ計算(重力・抗力・マグヌス力)
    /// </summary>
    private static void SimulatePhysicsStep(
        ref Vector3 position,
        ref Vector3 velocity,
        Vector3 spinAxisNormalized,
        float spinRateRPM,
        float liftCoefficient,
        float deltaTime)
    {
        Vector3 gravity = Physics.gravity;
        Vector3 dragForce = BallAerodynamics.CalculateDragForce(velocity);
        Vector3 magnusForce = BallAerodynamics.CalculateMagnusForce(
            velocity,
            spinAxisNormalized,
            spinRateRPM,
            liftCoefficient
        );

        Vector3 acceleration = gravity + (dragForce + magnusForce) / BALL_MASS_KG;

        velocity += acceleration * deltaTime;
        position += velocity * deltaTime;
    }

    /// <summary>
    /// 内部実装:軌道計算の共通ロジック
    /// </summary>
    private static TrajectoryResult SimulateTrajectoryInternal(
        Vector3 startPosition,
        Vector3 initialVelocity,
        Vector3 spinAxisNormalized,
        float spinRateRPM,
        float liftCoefficient,
        BallPhysicsCalculator.SimulationConfig config,
        bool trackGroundLayer)
    {
        List<Vector3> trajectory = new List<Vector3> { startPosition };
        Vector3 position = startPosition;
        Vector3 velocity = initialVelocity;

        float elapsed = 0f;
        int bounceCount = 0;
        bool isRolling = false;
        string firstGroundLayer = null;
        int landingIndex = 0;

        while (elapsed < config.MaxSimulationTime)
        {
            Vector3 prevPos = position;
            Vector3 newPos = position;
            Vector3 newVel = velocity;

            SimulatePhysicsStep(ref newPos, ref newVel, spinAxisNormalized,
                               spinRateRPM, liftCoefficient, config.DeltaTime);

            // フェンス反射
            if (!isRolling && config.BounceSettings != null)
            {
                BallCollisions.TryReflectOnFence(prevPos, ref newPos, ref newVel,
                                                config.DeltaTime,
                                                config.BounceSettings.wallRestitution);
            }

            // 地面衝突
            if (config.BounceSettings != null && newPos.y <= config.BounceSettings.groundLevel)
            {
                newPos.y = config.BounceSettings.groundLevel;

                if (trackGroundLayer && firstGroundLayer == null)
                {
                    firstGroundLayer = DetectGroundLayer(newPos, config.BounceSettings);
                    landingIndex = trajectory.Count;
                    bool shouldRoll = BallCollisions.HandleGroundBounce(
                        ref newVel, config.BounceSettings, bounceCount);

                    bounceCount++;
                    if (shouldRoll) isRolling = true;
                }
            }

            position = newPos;
            velocity = newVel;
            trajectory.Add(position);

            elapsed += config.DeltaTime;

            // 転がり処理
            if (isRolling && config.BounceSettings != null)
            {
                position = BallCollisions.SimulateRolling(
                    position, ref velocity, config.BounceSettings, config.DeltaTime);
                trajectory[trajectory.Count - 1] = position;

                if (velocity.magnitude < config.BounceSettings.stopVelocityThreshold)
                    break;
            }

            // 指定Z座標で停止
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

    /// <summary>
    /// 投球用(シンプル版)
    /// </summary>
    public static List<Vector3> SimulateTrajectory(
        Vector3 startPosition,
        Vector3 initialVelocity,
        Vector3 spinAxisNormalized,
        float spinRateRPM,
        float liftCoefficient,
        BallPhysicsCalculator.SimulationConfig config)
    {
        var result = SimulateTrajectoryInternal(
            startPosition, initialVelocity, spinAxisNormalized,
            spinRateRPM, liftCoefficient, config,
            trackGroundLayer: false);

        return result.Points;
    }

    /// <summary>
    /// 打球用
    /// </summary>
    public static TrajectoryResult SimulateTrajectoryWithMetadata(
        Vector3 startPosition,
        Vector3 initialVelocity,
        Vector3 spinAxisNormalized,
        float spinRateRPM,
        float liftCoefficient,
        BallPhysicsCalculator.SimulationConfig config)
    {
        return SimulateTrajectoryInternal(
            startPosition, initialVelocity, spinAxisNormalized,
            spinRateRPM, liftCoefficient, config,
            trackGroundLayer: true);
    }

    /// <summary>
    /// 地面に接地した際のレイヤーを判定
    /// ホームラン判定やフェア/ファール判定に使用
    /// </summary>
    private static string DetectGroundLayer(Vector3 position, BounceSettings settings)
    {
        if (settings == null)
            return LAYER_UNKNOWN;

        // 判定中心点(地面レベルに固定)
        Vector3 center = new Vector3(
            position.x,
            settings.groundLevel,
            position.z
        );

        // 判定対象レイヤー
        int mask = LayerMask.GetMask(LAYER_GROUND, LAYER_HOMERUN_ZONE);
        if (mask == 0)
        {
            Debug.LogWarning($"[DetectGroundLayer] {LAYER_GROUND}/{LAYER_HOMERUN_ZONE} レイヤーが存在しません");
            mask = ~0; // すべてのレイヤーを対象
        }

        // 上から下へのRaycast
        if (Physics.Raycast(
            center + Vector3.up * RAYCAST_START_OFFSET,
            Vector3.down,
            out RaycastHit hit,
            RAYCAST_MAX_DISTANCE,
            mask,
            QueryTriggerInteraction.Collide))
        {
            string layerName = LayerMask.LayerToName(hit.collider.gameObject.layer);
            return layerName;
        }

        // 下から上へのRaycast(ボールが地面に埋まっている場合)
        if (Physics.Raycast(
            center + Vector3.down * RAYCAST_START_OFFSET,
            Vector3.up,
            out hit,
            RAYCAST_MAX_DISTANCE,
            mask,
            QueryTriggerInteraction.Collide))
        {
            string layerName = LayerMask.LayerToName(hit.collider.gameObject.layer);
            return layerName;
        }

        // Raycastが当たらない場合の保険(OverlapSphere)
        Collider[] colliders = Physics.OverlapSphere(
            center,
            GROUND_DETECTION_RADIUS,
            mask,
            QueryTriggerInteraction.Collide);

        if (colliders != null && colliders.Length > 0)
        {
            // 最も近いコライダーを採用
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