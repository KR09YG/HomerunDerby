using UnityEngine;

public static class BattingPhysics
{
    private const float BALL_MASS_KG = 0.145f;
    private const float EFFECTIVE_BAT_MASS_FACTOR = 0.7f;
    private const float KMH_TO_MS = 1f / 3.6f;
    private const float BALL_RADIUS_M = 0.0366f;
    private const float RPM_TO_RAD_PER_SEC = 2f * Mathf.PI / 60f;

    /// <summary>
    /// 打球初速を計算
    /// </summary>
    /// <param name="pitchSpeedMs">投球速度(m/s)</param>
    /// <param name="batSpeedKmh">バット速度(km/h)</param>
    /// <param name="batMass">バット質量(kg)</param>
    /// <param name="cor">反発係数</param>
    /// <param name="efficiency">インパクト効率</param>
    public static float CalculateExitVelocity(
        float pitchSpeedMs,
        float batSpeedKmh,
        float batMass,
        float cor,
        float efficiency)
    {
        // バット速度をm/sに変換
        float batSpeedMs = batSpeedKmh * KMH_TO_MS;
        // 有効バット質量を計算
        float effectiveBatMass = batMass * EFFECTIVE_BAT_MASS_FACTOR;
        // 打球初速計算
        float numerator =
            (BALL_MASS_KG - cor * effectiveBatMass) * pitchSpeedMs +
            effectiveBatMass * (1f + cor) * batSpeedMs;
        // 分母計算
        float denominator = BALL_MASS_KG + effectiveBatMass;
        // 最終的な打球初速に効率を乗算
        float baseVelocity = numerator / denominator;
        return baseVelocity * efficiency;
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
        // スイートスポット内かどうか
        if (impactDistance <= sweetSpotRadius)
            return 1.0f;

        // 空振りの場合（念のためクランプ）
        if (impactDistance >= maxImpactDistance)
            return efficiencyCurve.Evaluate(1.0f);

        // スイートスポット外の効率をカーブで計算
        float t = Mathf.InverseLerp(sweetSpotRadius, maxImpactDistance, impactDistance);
        return efficiencyCurve.Evaluate(t);
    }

    // ===== 打ち上げ角度計算用定数 =====
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
        float angleOffset = Mathf.Sign(normalizedOffset) *
                           Mathf.Pow(Mathf.Abs(normalizedOffset), param.LaunchAnglePower) *
                           param.LaunchAngleScale;

        float launchAngle = param.IdealLaunchAngle - angleOffset;
        return Mathf.Clamp(launchAngle, param.MinLaunchAngle, param.MaxLaunchAngle);
    }

    /// <summary>
    /// 水平角度を計算
    /// </summary>
    public static float CalculateHorizontalAngle(float timing, BattingParameters param)
    {
        // タイミングがファウル閾値を超える場合はファウル角度を計算
        if (Mathf.Abs(timing) > param.FoulThreshold)
        {
            float excessTiming = (Mathf.Abs(timing) - param.FoulThreshold) / (1f - param.FoulThreshold);
            float foulAngle = Mathf.Lerp(param.MaxFairAngle, param.MaxFoulAngle, excessTiming);
            return Mathf.Sign(timing) * foulAngle;
        }

        // タイミングに基づいて水平角度を計算
        // 早めなら左、遅めなら右に曲がるようにする
        float normalizedTiming = timing / param.FoulThreshold;
        return normalizedTiming * param.MaxFairAngle;
    }

    /// <summary>
    /// スピン量を計算
    /// </summary>
    public static float CalculateSpinRate(
        float exitVelocity,
        float launchAngle,
        float efficiency,
        BattingParameters param)
    {
        float velocityFactor = exitVelocity / 40f;
        float angleFactor = 1f + (launchAngle / 30f) * 0.3f;
        float spinRate = param.BaseBackspinRPM * velocityFactor * angleFactor * efficiency;

        return Mathf.Clamp(spinRate, param.MinSpinRate, param.MaxSpinRate);
    }

    /// <summary>
    /// 揚力係数を計算
    /// </summary>
    public static float CalculateLiftCoefficient(
        float spinRateRpm,
        float speedMs,
        BattingParameters param)
    {
        float omega = spinRateRpm * RPM_TO_RAD_PER_SEC;
        float spinRatio = (omega * BALL_RADIUS_M) / speedMs;

        float cl = (param.LiftCoefficientA * spinRatio) / (param.LiftCoefficientB + spinRatio);
        return Mathf.Clamp(cl, 0f, param.MaxLiftCoefficient);
    }

    /// <summary>
    /// 打球方向ベクトルを計算
    /// </summary>
    public static Vector3 CalculateBattedBallDirection(float launchAngle, float horizontalAngle)
    {
        // 打ち上げ角度と水平角度から方向ベクトルを計算
        float launchRad = launchAngle * Mathf.Deg2Rad;
        float horizontalRad = horizontalAngle * Mathf.Deg2Rad;

        float x = Mathf.Sin(horizontalRad) * Mathf.Cos(launchRad);
        float y = Mathf.Sin(launchRad);
        float z = -Mathf.Cos(horizontalRad) * Mathf.Cos(launchRad);

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