using UnityEngine;

public static class BattingPhysics
{
    private const float EffectiveBatMassFactor = 0.7f;
    private const float MinOffsetMeters = 0.001f;
    private const float SpinSpeedReferenceMps = 40f;
    private const float SpinAngleReferenceDegrees = 30f;
    private const float SpinAngleScale = 0.3f;
    private const float MinSpinAxisSquared = 1e-6f;

    // 打球方向に使う前後距離を基準平面から求める。
    public static float CalculateTiming(float ballZ, float zoneZ, float timingRangeMeters = 0.5f)
    {
        return Mathf.Clamp(BaseballWorldScale.ToMeters(ballZ - zoneZ) / Mathf.Max(MinOffsetMeters, timingRangeMeters), -1f, 1f);
    }

    /// <summary>投球とバットの衝突から打球初速を求める。</summary>
    /// <param name="pitchSpeedMps">投球速度（m/s）</param>
    /// <param name="batSpeedKmh">バット速度（km/h）</param>
    /// <param name="batMass">バット質量（kg）</param>
    /// <param name="cor">反発係数</param>
    /// <param name="efficiency">インパクト効率</param>
    /// <returns>打球初速（m/s）</returns>
    public static float CalculateExitVelocity(
        float pitchSpeedMps,
        float batSpeedKmh,
        float batMass,
        float cor,
        float efficiency)
    {
        float batSpeedMps = batSpeedKmh * BallPhysicsConstants.KPH_TO_MPS;
        float effectiveBatMass = batMass * EffectiveBatMassFactor;
        // 両方の引数は速さ。投球とバットが逆向きに動く衝突として扱う。
        float numerator =
            (cor * effectiveBatMass - BallPhysicsConstants.BALL_MASS_KG) * pitchSpeedMps +
            effectiveBatMass * (1f + cor) * batSpeedMps;
        float denominator = BallPhysicsConstants.BALL_MASS_KG + effectiveBatMass;
        float baseVelocityMps = numerator / denominator;
        return Mathf.Max(0f, baseVelocityMps) * Mathf.Clamp01(efficiency);
    }

    /// <summary>芯からの距離に応じたインパクト効率を求める。</summary>
    /// <param name="impactDistance">芯とボールの距離（m）</param>
    /// <param name="sweetSpotRadius">芯として扱う範囲の半径（m）</param>
    /// <param name="maxImpactDistance">接触判定の最大距離（m）</param>
    /// <param name="efficiencyCurve">芯の外側で使う効率曲線</param>
    /// <param name="minEfficiency">効率の下限</param>
    public static float CalculateImpactEfficiency(
        float impactDistance,
        float sweetSpotRadius,
        float maxImpactDistance,
        AnimationCurve efficiencyCurve,
        float minEfficiency = 0f)
    {
        if (impactDistance <= sweetSpotRadius)
            return 1.0f;

        // 判定範囲の外側ではカーブの終端値を使う。
        if (impactDistance >= maxImpactDistance)
            return Mathf.Clamp(efficiencyCurve.Evaluate(1.0f), Mathf.Clamp01(minEfficiency), 1f);

        float t = Mathf.InverseLerp(sweetSpotRadius, maxImpactDistance, impactDistance);
        return Mathf.Clamp(efficiencyCurve.Evaluate(t), Mathf.Clamp01(minEfficiency), 1f);
    }

    /// <summary>ボールとバットの上下のずれから打ち上げ角度を求める。</summary>
    public static float CalculateLaunchAngle(
        float verticalOffset,
        BattingParameters param)
    {
        float normalizedOffset = verticalOffset / Mathf.Max(MinOffsetMeters, param.LaunchAngleOffsetMeters);
        float angleOffsetDeg = Mathf.Sign(normalizedOffset) *
                           Mathf.Pow(Mathf.Abs(normalizedOffset), param.LaunchAnglePower) *
                           param.LaunchAngleScale;

        float launchAngleDeg = param.IdealLaunchAngle - angleOffsetDeg;
        return Mathf.Clamp(launchAngleDeg, param.MinLaunchAngle, param.MaxLaunchAngle);
    }

    /// <summary>接触タイミングから左右の打球角度を求める。</summary>
    public static float CalculateHorizontalAngle(float timing, BattingParameters param)
    {
        if (Mathf.Abs(timing) > param.FoulThreshold)
        {
            float excessTiming = (Mathf.Abs(timing) - param.FoulThreshold) / (1f - param.FoulThreshold);
            float foulAngleDeg = Mathf.Lerp(param.MaxFairAngle, param.MaxFoulAngle, excessTiming);
            return -Mathf.Sign(timing) * foulAngleDeg;
        }

        // ジャスト付近の小さな前後差では、打球方向が急に変わらないようにする。
        float normalizedTiming = Mathf.Clamp01(Mathf.Abs(timing) / param.FoulThreshold);
        float angleRatio = Mathf.Pow(normalizedTiming, Mathf.Max(1f, param.HorizontalAnglePower));
        return -Mathf.Sign(timing) * angleRatio * param.MaxFairAngle;
    }

    /// <summary>打球初速と打ち上げ角度から回転数を求める。</summary>
    public static float CalculateSpinRate(
        float exitVelocityMps,
        float launchAngleDeg,
        float efficiency,
        BattingParameters param)
    {
        float velocityFactor = exitVelocityMps / SpinSpeedReferenceMps;
        float angleFactor = 1f + (launchAngleDeg / SpinAngleReferenceDegrees) * SpinAngleScale;
        float calculatedSpinRateRpm = param.BaseBackspinRPM * velocityFactor * angleFactor * efficiency;

        return Mathf.Clamp(calculatedSpinRateRpm, param.MinSpinRate, param.MaxSpinRate);
    }

    /// <summary>回転数と球速から打球の揚力係数を求める。</summary>
    public static float CalculateLiftCoefficient(
        float spinRateRpm,
        float speedMps,
        BattingParameters param)
    {
        if (speedMps <= 0f || spinRateRpm <= 0f) return 0f;
        float angularVelocityRadPerSec = spinRateRpm * BallPhysicsConstants.RPM_TO_RAD_PER_SEC;
        float spinRatio = (angularVelocityRadPerSec * BallPhysicsConstants.BALL_RADIUS_M) / speedMps;

        float cl = (param.LiftCoefficientA * spinRatio) / (param.LiftCoefficientB + spinRatio);
        return Mathf.Clamp(cl, 0f, param.MaxLiftCoefficient);
    }

    /// <summary>打ち上げ角度と水平角度をワールド座標の打球方向に変換する。</summary>
    public static Vector3 CalculateBattedBallDirection(float launchAngleDeg, float horizontalAngleDeg)
    {
        float launchAngleRad = launchAngleDeg * Mathf.Deg2Rad;
        float horizontalAngleRad = horizontalAngleDeg * Mathf.Deg2Rad;

        float x = Mathf.Sin(horizontalAngleRad) * Mathf.Cos(launchAngleRad);
        float y = Mathf.Sin(launchAngleRad);
        float z = -Mathf.Cos(horizontalAngleRad) * Mathf.Cos(launchAngleRad);

        return new Vector3(x, y, z).normalized;
    }

    /// <summary>打球のバックスピンに、接触タイミングによる横回転を加えた軸を求める。</summary>
    public static Vector3 CalculateSpinAxis(Vector3 direction, float timing, BattingParameters param)
    {
        // 打球の進行方向に直交する軸を作り、引っ張り・流しでも上向きの揚力を保つ。
        Vector3 forward = direction.normalized;
        if (forward.sqrMagnitude < MinSpinAxisSquared) return Vector3.zero;
        Vector3 backspinAxis = Vector3.Cross(forward, Vector3.up).normalized;
        if (backspinAxis.sqrMagnitude < MinSpinAxisSquared)
            backspinAxis = Vector3.Cross(forward, Vector3.right).normalized;
        // タイミングによる軸の傾きで、引っ張りと流しの横回転を表す。
        float sideSpinAngleDeg = -timing * param.MaxSideSpinAngle;
        Quaternion sideRotation = Quaternion.AngleAxis(sideSpinAngleDeg, forward);
        Vector3 spinAxis = sideRotation * backspinAxis;

        return spinAxis.normalized;
    }
}
