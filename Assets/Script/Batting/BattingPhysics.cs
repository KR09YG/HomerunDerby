using UnityEngine;

public static class BattingPhysics
{
    private const float EffectiveBatMassFactor = 0.7f;
    private const float SpinSpeedReferenceMps = 40f;
    private const float SpinAngleReferenceDegrees = 30f;
    private const float SpinAngleScale = 0.3f;

    /// <summary>
    /// 打球初速を計算
    /// </summary>
    /// <param name="pitchSpeedMps">投球速度(m/s)</param>
    /// <param name="batSpeedKmh">バット速度(km/h)</param>
    /// <param name="batMass">バット質量(kg)</param>
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
        // 打球初速計算
        float numerator =
            (BallPhysicsConstants.BALL_MASS_KG - cor * effectiveBatMass) * pitchSpeedMps +
            effectiveBatMass * (1f + cor) * batSpeedMps;
        float denominator = BallPhysicsConstants.BALL_MASS_KG + effectiveBatMass;
        float baseVelocityMps = numerator / denominator;
        return baseVelocityMps * efficiency;
    }

    /// <summary>
    /// インパクト効率を計算
    /// </summary>
    ///<param name="impactDistance">インパクト距離(m)</param>
    ///<param name="sweetSpotRadius">スイートスポット半径(m)</param>
    ///<param name="maxImpactDistance">最大インパクト距離(m)</param>
    ///<param name="efficiencyCurve">インパクト効率カーブ</param>
    public static float CalculateImpactEfficiency(
        float impactDistance,
        float sweetSpotRadius,
        float maxImpactDistance,
        AnimationCurve efficiencyCurve)
    {
        if (impactDistance <= sweetSpotRadius)
            return 1.0f;

        // 判定範囲の外側ではカーブの終端値を使う。
        if (impactDistance >= maxImpactDistance)
            return efficiencyCurve.Evaluate(1.0f);

        float t = Mathf.InverseLerp(sweetSpotRadius, maxImpactDistance, impactDistance);
        return efficiencyCurve.Evaluate(t);
    }

    // 打ち上げ角度の補正に使う基準距離。
    // オフセットスケール
    private const float MAX_VERTICAL_OFFSET = 0.1f;
    /// <summary>
    /// 打ち上げ角度を計算
    /// </summary>
    public static float CalculateLaunchAngle(
        float verticalOffset,
        BattingParameters param)
    {
        // オフセットに基づいて角度補正を計算
        float normalizedOffset = verticalOffset / MAX_VERTICAL_OFFSET;
        // 角度のズレをべき乗で計算
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
            return Mathf.Sign(timing) * foulAngleDeg;
        }

        // タイミングに基づいて水平角度を計算
        // 早めなら左、遅めなら右に曲がるようにする
        float normalizedTiming = timing / param.FoulThreshold;
        return normalizedTiming * param.MaxFairAngle;
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

    /// <summary>
    /// スピン軸を計算
    /// </summary>
    public static Vector3 CalculateSpinAxis(Vector3 direction)
    {
        Vector3 horizontalDirection = new Vector3(direction.x, 0, direction.z).normalized;
        Vector3 spinAxis = Vector3.Cross(horizontalDirection, Vector3.up).normalized;

        if (spinAxis.sqrMagnitude < 0.01f)
            spinAxis = Vector3.right;

        return spinAxis;
    }
}