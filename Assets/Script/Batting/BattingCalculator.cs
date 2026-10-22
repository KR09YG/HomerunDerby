using System.Collections.Generic;
using UnityEngine;

public class BattingCalculator : MonoBehaviour
{
    [Header("参照")]
    private BattingSystem _battingSystem;
    [SerializeField] private StrikeZone _strikeZone;
    [SerializeField] private Transform _fieldReference;
    [SerializeField] private BattingParameters _parameters;
    [SerializeField] private BounceSettings _bounceSettings;

    [Header("イベント")]
    [SerializeField] private OnBattingHitEvent _hitEvent;
    [SerializeField] private OnBattingResultEvent _battedBallResultEvent;
    private const float GroundBallAngleDegrees = 10f;
    private const float FlightStepSeconds = 0.01f;
    private const float MaxFlightSeconds = 10f;

    private List<Vector3> _lastTrajectory;
    private BattingBallType _lastBallType;
    public StrikeZone Zone => _strikeZone;

    private void Awake()
    {
        _battingSystem = GetComponent<BattingSystem>();
        if (_hitEvent != null) _hitEvent.RegisterListener(OnHitAttempt);
        else Debug.LogError("[BattingCalculator] OnBattingHitEventが設定されていません");
    }

    private void OnDestroy()
    {
        _hitEvent?.UnregisterListener(OnHitAttempt);
    }

    private void OnHitAttempt(PitchBallMove ball)
    {
        // 停止中に座標が変わっても、判定には打撃イベント時の記録を使う。
        if (_battingSystem == null || !_battingSystem.TryGetContact(ball, out Vector3 cursorPos, out Vector3 ballPos))
        {
            Debug.LogWarning("[Batting/Result] MISS: no contact snapshot (ball or cursor position unavailable).", this);
            RaiseMissEvent();
            return;
        }

        if (_parameters == null)
        {
            Debug.LogError("[Batting/Result] MISS: BattingParameters is not assigned.", this);
            RaiseMissEvent();
            return;
        }

        Vector3 offsetMeters = BaseballWorldScale.ToMeters(ballPos - cursorPos);
        float distance = offsetMeters.magnitude;
        bool isHit = distance <= _parameters.MaxImpactDistance;

        if (_parameters.EnableDebugLogs)
            Debug.Log($"[Batting/Result] {(isHit ? "HIT" : "MISS")} distance={distance:F4}m " +
                $"limit={_parameters.MaxImpactDistance:F4}m deltaXYZ(m)={offsetMeters.ToString("F4")}", this);

        if (isHit)
        {
            CalculateBattedBall(ball, cursorPos, ballPos, distance);
        }
        else
        {
            RaiseMissEvent();
        }
    }

    private void CalculateBattedBall(PitchBallMove ball, Vector3 cursorPos, Vector3 ballPos, float impactDistance)
    {
        if (!_battingSystem.TryGetContactVelocity(ball, out Vector3 pitchVelocityMps))
        {
            Debug.LogWarning("[Batting/Result] 接触時の投球速度を取得できないため、打球計算を中止しました。", this);
            RaiseMissEvent();
            return;
        }
        // 当たり方は接触時の座標で決め、飛翔だけをバット上の表示位置から始める。
        Vector3 launchPosition = _battingSystem.TryGetContactLaunchPosition(ball, out Vector3 contactPoint)
            ? contactPoint : ballPos;
        float pitchSpeed = pitchVelocityMps.magnitude;
        float timing = CalculateTiming(ballPos);
        // 接触効率と打球初速の倍率は別に扱う。
        float efficiency = BattingPhysics.CalculateImpactEfficiency(
            impactDistance,
            _parameters.SweetSpotRadius,
            _parameters.MaxImpactDistance,
            _parameters.ImpactEfficiencyCurve,
            _parameters.MinImpactEfficiency
        );

        float exitVelocity = BattingPhysics.CalculateExitVelocity(
            pitchSpeed,
            _parameters.BatSpeedKmh,
            _parameters.BatMass,
            _parameters.CoefficientOfRestitution,
            efficiency
        ) * _parameters.ExitVelocityScale;

        float verticalOffset = BaseballWorldScale.ToMeters(cursorPos.y - ballPos.y);
        float launchAngle = BattingPhysics.CalculateLaunchAngle(verticalOffset, _parameters);
        float horizontalAngle = BattingPhysics.CalculateHorizontalAngle(timing, _parameters);

        float spinRate = BattingPhysics.CalculateSpinRate(exitVelocity, launchAngle, efficiency, _parameters);

        Vector3 direction = BattingPhysics.CalculateBattedBallDirection(launchAngle, horizontalAngle);
        Vector3 spinAxis = BattingPhysics.CalculateSpinAxis(direction, timing, _parameters);
        if (_parameters.EnableDebugLogs) Debug.Log($"[Spin] spinAxis={spinAxis}");
        Vector3 initialVelocity = direction * exitVelocity;

        // 初期値を渡し、飛翔中の揚力係数はシミュレーターで更新する。
        float liftCoefficient = BattingPhysics.CalculateLiftCoefficient(spinRate, exitVelocity, _parameters);

        BallPhysicsCalculator.SimulationConfig simulateConfig =
            new BallPhysicsCalculator.SimulationConfig
            {
                DeltaTime = FlightStepSeconds,
                MaxSimulationTimeSeconds = MaxFlightSeconds,
                StopAtZ = null,
                BounceSettings = _bounceSettings,
                BattingParameters = _parameters
            };

        TrajectoryResult simulation = BallPhysicsCalculator.SimulateBattedBallWithStadiumInfo(
            launchPosition, initialVelocity,
            spinAxis, spinRate,
            liftCoefficient, simulateConfig);
        List<Vector3> trajectory = simulation.Points;
        string firstGroundLayer = simulation.FirstGroundLayer;
        int landingIndex = simulation.LandingIndex;

        if (trajectory == null || trajectory.Count == 0)
        {
            Debug.LogError("[Batting] 打球軌道を生成できませんでした。", this);
            RaiseMissEvent();
            return;
        }

        _lastTrajectory = trajectory;

        bool hasGroundContact = firstGroundLayer != null;
        int firstGroundIndex = hasGroundContact ? landingIndex : -1;
        int terminalIndex = hasGroundContact ? landingIndex : trajectory.Count - 1;
        if (_fieldReference == null)
            Debug.LogWarning("[Batting] 球場の基準Transformが未設定です。ワールド原点を本塁として判定します。", this);
        Vector3 fieldOrigin = _fieldReference != null ? _fieldReference.position : Vector3.zero;
        Vector3 fieldForward = _fieldReference != null ? -_fieldReference.forward : Vector3.back;
        FairFoulDecision fairFoul = FairFoulJudge.Evaluate(
            trajectory, firstGroundIndex, fieldOrigin, fieldForward, _parameters.MaxFairAngle,
            BaseballWorldScale.ToWorldUnits(_parameters.BaseDistanceMeters));
        bool crossedFence = StadiumHomeRunJudge.TryFindFenceCrossing(
            trajectory, firstGroundIndex, fieldOrigin, fieldForward,
            _parameters.MaxFairAngle, out int fenceCrossingIndex);
        bool hitFoulPole = simulation.FoulPoleContactIndex >= 0 &&
            (firstGroundIndex < 0 || simulation.FoulPoleContactIndex <= firstGroundIndex);
        int homeRunIndex = hitFoulPole && crossedFence
            ? Mathf.Min(simulation.FoulPoleContactIndex, fenceCrossingIndex)
            : hitFoulPole ? simulation.FoulPoleContactIndex : fenceCrossingIndex;
        int resolutionIndex = hitFoulPole || crossedFence
            ? homeRunIndex : Mathf.Max(terminalIndex, fairFoul.ResolutionIndex);

        if (!hasGroundContact)
            Debug.LogWarning("[Batting] 軌道の終了まで接地しませんでした。終端位置で打球結果を判定します。", this);

        float distance = CalculateDistance(launchPosition, trajectory[terminalIndex]);

        BattingBallType ballType = DetermineBallType(
            distance, launchAngle, hasGroundContact, fairFoul.IsFoul, crossedFence || hitFoulPole);
        _lastBallType = ballType;

        BattingBallResult result = new BattingBallResult(ballType)
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
            Distance = distance,
            LandingIndex = terminalIndex,
            ResolutionIndex = resolutionIndex,
            HasGroundContact = hasGroundContact
        };


        if (_parameters.EnableDebugLogs)
        {
            Debug.Log($"[Batting] ContactPitch={pitchSpeed * BallPhysicsConstants.MPS_TO_KPH:F1}km/h, " +
                $"ExitVel={exitVelocity * BallPhysicsConstants.MPS_TO_KPH:F1}km/h, Efficiency={efficiency:F3}, " +
                $"Spin={spinRate:F0}rpm, Launch={launchAngle:F1}°, Timing={timing:F3}, " +
                $"Horizontal={horizontalAngle:F1}°, FoulAngle={fairFoul.AngleDegrees:F1}°, " +
                $"Distance={distance:F1}m, Type={ballType}, Layer={firstGroundLayer ?? "None"}, " +
                $"CrossedFence={crossedFence}, FenceIndex={fenceCrossingIndex}, " +
                $"HitFoulPole={hitFoulPole}, PoleIndex={simulation.FoulPoleContactIndex}, " +
                $"DecisionIndex={fairFoul.ResolutionIndex}, DecisionPos={fairFoul.Position.ToString("F2")}");
        }

        _battedBallResultEvent?.RaiseEvent(result);
    }

    private float CalculateTiming(Vector3 ballPosition)
    {
        return BattingPhysics.CalculateTiming(ballPosition.z, _strikeZone.CenterZ, _parameters.TimingRangeMeters);
    }

    private float CalculateDistance(Vector3 start, Vector3 end)
    {
        return BaseballWorldScale.ToMeters(Vector3.Distance(
            new Vector3(start.x, 0, start.z),
            new Vector3(end.x, 0, end.z)
        ));
    }

    private BattingBallType DetermineBallType(
        float distance,
        float launchAngle,
        bool hasGroundContact,
        bool isFoul,
        bool crossedFence)
    {
        if (crossedFence)
            return BattingBallType.HomeRun;

        if (isFoul)
            return BattingBallType.Foul;

        // 接地していない打球を、レイヤーがないという理由で本塁打にしない。
        if (!hasGroundContact)
            return launchAngle < GroundBallAngleDegrees ? BattingBallType.GroundBall : BattingBallType.Hit;

        if (launchAngle < GroundBallAngleDegrees)
            return BattingBallType.GroundBall;

        if (distance >= _parameters.HitDistance)
            return BattingBallType.Hit;

        return BattingBallType.GroundBall;
    }

    private void RaiseMissEvent()
    {
        _battedBallResultEvent?.RaiseEvent(new BattingBallResult(BattingBallType.Miss));
    }

    private void OnDrawGizmos()
    {
        if (_parameters == null || !_parameters.ShowTrajectoryGizmos) return;
        if (_lastTrajectory == null || _lastTrajectory.Count == 0) return;

        Color trajectoryColor = _lastBallType == BattingBallType.HomeRun ? Color.yellow : Color.green;
        Gizmos.color = trajectoryColor;

        for (int i = 0; i < _lastTrajectory.Count - 1; i++)
        {
            Gizmos.DrawLine(_lastTrajectory[i], _lastTrajectory[i + 1]);
        }

        if (_lastTrajectory.Count > 0)
        {
            Gizmos.DrawWireSphere(_lastTrajectory[_lastTrajectory.Count - 1], 1f);
        }
    }
}
