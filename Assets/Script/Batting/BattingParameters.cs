using UnityEngine;

[CreateAssetMenu(fileName = "BattingParam", menuName = "Batting Param")]
public class BattingParameters : ScriptableObject
{
    [Header("=== 当たり判定 ===")]
    [Tooltip("操作上ヒットを許容する、カーソルからの最大距離(m)")]
    public float MaxImpactDistance = 0.5f;

    [Tooltip("スイートスポット半径(m)")]
    public float SweetSpotRadius = 0.08f;

    [Header("=== バット性能 ===")]
    [Tooltip("バット速度(km/h)")]
    public float BatSpeedKmh = 116f;

    [Tooltip("バット質量(kg)")]
    public float BatMass = 0.9f;

    [Tooltip("反発係数")]
    [Range(0.4f, 0.6f)]
    public float CoefficientOfRestitution = 0.5f;

    [Header("=== 打球速度調整 ===")]
    [Tooltip("打球初速の全体スケール")]
    [Range(0.5f, 1.5f)]
    public float ExitVelocityScale = 1.0f;

    [Tooltip("芯ズレ時の最小効率")]
    [Range(0.01f, 0.2f)]
    public float MinImpactEfficiency = 0.05f;

    [Tooltip("芯ズレ減衰カーブ")]
    public AnimationCurve ImpactEfficiencyCurve = AnimationCurve.EaseInOut(0, 1, 1, 0.05f);

    [Header("=== 打ち上げ角度 ===")]
    [Tooltip("理想的な打ち上げ角度(度)")]
    [Range(15f, 35f)]
    public float IdealLaunchAngle = 25f;

    [Tooltip("角度スケール")]
    [Range(10f, 30f)]
    public float LaunchAngleScale = 25f;

    [Tooltip("角度計算のべき乗")]
    [Range(0.8f, 2.0f)]
    public float LaunchAnglePower = 1.2f;

    [Tooltip("LaunchAngleScale分だけ角度が変わる上下のずれ(m)"), Min(0.001f)]
    public float LaunchAngleOffsetMeters = 0.15f;

    [Tooltip("最小打ち上げ角度(度)")]
    public float MinLaunchAngle = -20f;

    [Tooltip("最大打ち上げ角度(度)")]
    public float MaxLaunchAngle = 55f;

    [Header("=== 水平角度 ===")]
    [Tooltip("フェア最大角度(度)")]
    public float MaxFairAngle = 45f;

    [Tooltip("ファール最大角度(度)")]
    public float MaxFoulAngle = 60f;

    [Tooltip("ファール判定閾値")]
    [Range(0.5f, 0.9f)]
    public float FoulThreshold = 0.7f;

    [Tooltip("ジャスト付近の前後差が左右角度へ与える影響。1で直線的に変化")]
    [Range(1f, 3f)]
    public float HorizontalAnglePower = 1.5f;

    [Tooltip("左右の打球角度を決める前後距離の基準(m)。大きくすると同じ前後差でも角度が変わりにくくなります。"), Min(0.001f)]
    public float TimingRangeMeters = 0.5f;

    [Header("=== スピン ===")]
    [Tooltip("基本バックスピン(RPM)")]
    public float BaseBackspinRPM = 1800f;

    [Tooltip("スピン最小値(RPM)")]
    public float MinSpinRate = 500f;

    [Tooltip("スピン最大値(RPM)")]
    public float MaxSpinRate = 4000f;

    [Tooltip("タイミングのズレ最大時のサイドスピン軸の傾き角度(度)"), Range(0f, 45f)]
    public float MaxSideSpinAngle = 10f;

    [Header("=== 揚力 ===")]
    [Tooltip("最大揚力係数")]
    [Range(0.2f, 0.8f)]
    public float MaxLiftCoefficient = 0.4f;

    [Tooltip("Cl = A × S / (B + S) のA。Sは球速に対する表面回転速度の比。")]
    public float LiftCoefficientA = 0.75f;

    [Tooltip("Cl = A × S / (B + S) のB。初期値は投球用の式と同じ係数になる。")]
    public float LiftCoefficientB = 0.5f;


    [Tooltip("ヒット距離(m)")]
    public float HitDistance = 30f;

    [Header("=== デバッグ ===")]
    public bool EnableDebugLogs = true;
    public bool ShowTrajectoryGizmos = true;
}
