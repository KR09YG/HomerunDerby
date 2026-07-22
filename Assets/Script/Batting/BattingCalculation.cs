using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using KanKikuchi.AudioManager;

public class BattingCalculator : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private BattingCursor _cursor;
    [SerializeField] private StrikeZone _strikeZone;
    [SerializeField] private BattingParameters _parameters;
    [SerializeField] private BounceSettings _bounceSettings;

    [Header("イベント")]
    [SerializeField] private OnBattingHitEvent _hitEvent;
    [SerializeField] private OnBattingResultEvent _battedBallResultEvent;
    // 打ち上げ角がこの値以下ならゴロと判定
    private const float GROUNDER_ANGLE_THRESHOLD = 10f;

    // デバッグ用
    private List<Vector3> _lastTrajectory;
    private string _lastGroundLayer;

    private void Awake()
    {
        if (_hitEvent != null) _hitEvent.RegisterListener(OnHitAttempt);
        else Debug.LogError("[BattingCalculator] OnBattingHitEventが設定されていません");
    }

    private void OnDestroy()
    {
        _hitEvent?.UnregisterListener(OnHitAttempt);
    }

    private void OnHitAttempt(PitchBallMove ball)
    {
        Vector3 swingPos = _cursor.CurrentPos;
        Vector3 ballPos = ball.transform.position;

        float distance = Vector3.Distance(swingPos, ballPos);
        bool isHit = distance <= _parameters.MaxImpactDistance;

        Debug.Log($"[Batting] SwingPos={swingPos}, BallPos={ballPos}, Distance={distance:F2}, Hit={isHit}");

        if (isHit)
        {
            SEManager.Instance.Play(SEPath.BATTING_HIT);
            CalculateBattedBall(ball, swingPos, ballPos, distance);
        }
        else
        {
            Debug.Log("Miss Swing");
            RaiseMissEvent();
        }
    }

    private void CalculateBattedBall(PitchBallMove ball, Vector3 swingPos, Vector3 ballPos, float impactDistance)
    {
        // 1. 基本パラメータ計算
        float pitchSpeed = GetPitchSpeed(ball);
        float timing = CalculateTiming(ball);

        // 2. 効率計算(芯に近いほど高効率)
        float efficiency = BattingPhysics.CalculateImpactEfficiency(
            impactDistance,
            _parameters.SweetSpotRadius,
            _parameters.MaxImpactDistance,
            _parameters.ImpactEfficiencyCurve
        );

        // 飛距離の調整
        efficiency *= _parameters.ExitVelocityScale;

        // 3. 打球速度計算
        float exitVelocity = BattingPhysics.CalculateExitVelocity(
            pitchSpeed,
            _parameters.BatSpeedKmh,
            _parameters.BatMass,
            _parameters.CoefficientOfRestitution,
            efficiency
        );

        // 4. 角度計算
        // ボールとバットの芯のずれを垂直方向のオフセットとして使用
        float verticalOffset = swingPos.y - ballPos.y;
        // 打ち上げ角度を計算
        float launchAngle = BattingPhysics.CalculateLaunchAngle(verticalOffset, _parameters);
        // 水平角度を計算
        float horizontalAngle = BattingPhysics.CalculateHorizontalAngle(timing, _parameters);

        // 5. スピン計算
        float spinRate = BattingPhysics.CalculateSpinRate(exitVelocity, launchAngle, efficiency, _parameters);

        // 6. 方向計算
        Vector3 direction = BattingPhysics.CalculateBattedBallDirection(launchAngle, horizontalAngle);
        Vector3 spinAxis = BattingPhysics.CalculateSpinAxis(direction);
        Vector3 initialVelocity = direction * exitVelocity;

        // 7. 軌道シミュレーション（レイヤー情報付き）
        float liftCoefficient = BattingPhysics.CalculateLiftCoefficient(spinRate, exitVelocity, _parameters);

        BallPhysicsCalculator.SimulationConfig simulateConfig =
            new BallPhysicsCalculator.SimulationConfig
            {
                DeltaTime = 0.01f,
                MaxSimulationTime = 10f,
                StopAtZ = null,
                BounceSettings = _bounceSettings
            };

        var (trajectory, firstGroundLayer, landingIndex) =
            BallPhysicsCalculator.SimulateTrajectoryWithGroundInfo(
            ballPos, initialVelocity,
            spinAxis, spinRate,
            liftCoefficient, simulateConfig);

        // 弾道を保存
        _lastTrajectory = trajectory;

        // 8. 結果判定
        float distance = CalculateDistance(ballPos, trajectory[landingIndex]);

        BattingBallType ballType = DetermineBallType(
            trajectory, ballPos, distance, launchAngle, horizontalAngle, firstGroundLayer, landingIndex);

        // 9. 結果オブジェクト作成
        BattingBallResult result = new BattingBallResult
        {
            InitialVelocity = initialVelocity,
            ExitVelocity = exitVelocity,
            LaunchAngle = launchAngle,
            HorizontalAngle = horizontalAngle,
            SpinAxis = spinAxis,
            SpinRate = spinRate,
            ImpactDistance = impactDistance,
            ImpactEfficiency = efficiency,
            IsSweetSpot = impactDistance <= _parameters.SweetSpotRadius,
            Timing = timing,
            TrajectoryPoints = trajectory,
            BallType = ballType,
            Distance = distance,
            LandingIndex = landingIndex
        };

        // 10. イベント発火
        _battedBallResultEvent?.RaiseEvent(result);

        if (_parameters.EnableDebugLogs)
        {
            Debug.Log($"[Batting] ExitVel={exitVelocity * 3.6f:F0}km/h, Angle={launchAngle:F1}°, Distance={distance:F1}m, Type={ballType}, Layer={firstGroundLayer}");
        }
    }


    private float GetPitchSpeed(PitchBallMove ball)
    {
        var traj = ball.Trajectory;
        if (traj.Count < 2) return 30f;

        float totalTime = (traj.Count - 1) * 0.01f;
        return (traj[traj.Count - 1] - traj[0]).magnitude / totalTime;
    }

    private float CalculateTiming(PitchBallMove ball)
    {
        float distanceToZone = ball.transform.position.z - _strikeZone.CenterZ;
        // タイミングは-1（早すぎ）から1（遅すぎ）までの範囲で、距離が近いほど0に近づく
        return Mathf.Clamp(distanceToZone / 2f, -1f, 1f);
    }

    private float CalculateDistance(Vector3 start, Vector3 end)
    {
        return Vector3.Distance(
            new Vector3(start.x, 0, start.z),
            new Vector3(end.x, 0, end.z)
        );
    }

    /// <summary>
    /// ファール判定
    /// </summary>
    private bool IsFoulBallByTrajectory(List<Vector3> trajectory, Vector3 startPosition, int landingIndex)
    {
        if (trajectory == null)
            return false;

        Vector3 earlyPoint = trajectory[landingIndex];

        Vector3 direction = earlyPoint - startPosition;
        direction.y = 0;

        if (direction.sqrMagnitude < 0.001f)
            return false;

        direction.Normalize();

        float angle = Mathf.Atan2(direction.x, -direction.z) * Mathf.Rad2Deg;
        bool isFoul = Mathf.Abs(angle) > _parameters.MaxFairAngle;

        if (_parameters.EnableDebugLogs && isFoul)
        {
            Debug.Log($"[Foul] Angle={angle:F1}°, Threshold=±{_parameters.MaxFairAngle}°");
        }

        return isFoul;
    }

    /// <summary>
    /// 打球タイプ判定
    /// </summary>
    private BattingBallType DetermineBallType(
        List<Vector3> trajectory,
        Vector3 startPosition,
        float distance,
        float launchAngle,
        float horizontalAngle,
        string firstGroundLayer,
        int landingIndex)
    {
        if (trajectory == null || trajectory.Count == 0)
            return BattingBallType.Miss;

        // 1. ファール判定
        if (IsFoulBallByTrajectory(trajectory, startPosition, landingIndex))
            return BattingBallType.Foul;

        // 2. ホームラン判定
        if (firstGroundLayer != "Ground" && firstGroundLayer != "Net")
        {
            if (_parameters.EnableDebugLogs)
            {
                Debug.Log($"[HomeRun] Landed on layer: {firstGroundLayer}");
            }
            return BattingBallType.HomeRun;
        }

        // 3. ゴロ判定
        if (launchAngle < 10f)
            return BattingBallType.GroundBall;

        // 4. ヒット判定
        if (distance >= _parameters.HitDistance)
            return BattingBallType.Hit;

        return BattingBallType.GroundBall;
    }

    private void RaiseMissEvent()
    {
        _battedBallResultEvent?.RaiseEvent(new BattingBallResult
        {
            BallType = BattingBallType.Miss
        });
    }

    private void OnDrawGizmos()
    {
        if (!_parameters.ShowTrajectoryGizmos) return;
        if (_lastTrajectory == null || _lastTrajectory.Count == 0) return;

        // 軌道を色分け
        Color trajectoryColor = _lastGroundLayer == "HomerunZone" ? Color.yellow : Color.green;
        Gizmos.color = trajectoryColor;

        for (int i = 0; i < _lastTrajectory.Count - 1; i++)
        {
            Gizmos.DrawLine(_lastTrajectory[i], _lastTrajectory[i + 1]);
        }

        // 着地点を強調
        if (_lastTrajectory.Count > 0)
        {
            Gizmos.DrawWireSphere(_lastTrajectory[_lastTrajectory.Count - 1], 1f);
        }
    }
}