using UnityEngine;

internal static class BallPhysicsConstants
{
    // 投球・打球・衝突の計算で共通のボールと空気の値。
    internal const float AIR_DENSITY_KG_PER_M3 = 1.225f; // kg/m³
    internal const float BALL_MASS_KG = 0.145f; // kg
    internal const float BALL_RADIUS_M = 0.0366f; // m
    internal const float CROSS_SECTION_M2 = Mathf.PI * BALL_RADIUS_M * BALL_RADIUS_M; // m²
    internal const float DRAG_COEFFICIENT = 0.3f;
    internal const float GRAVITY_HALF = 0.5f;
    internal const float MAGNUS_FORCE_HALF = 0.5f;
    internal const float DRAG_FORCE_HALF = 0.5f;

    // 球種やバットの設定値を、物理計算に使う単位へ変換する。
    internal const float KPH_TO_MPS = 1f / 3.6f;
    internal const float RPM_TO_RAD_PER_SEC = 2f * Mathf.PI / 60f;

    // 最適化パラメータ
    internal const int MAX_OPTIMIZATION_ITERATIONS = 20;
    internal const float POSITION_TOLERANCE = 0.02f;

    // シミュレーション終了条件
    internal const float GROUND_LEVEL = -0.5f;
    internal const int MAX_TRAJECTORY_POINTS = 10000;

    // 物理計算の閾値
    internal const float MIN_VELOCITY_SQUARED = 0.01f;
    internal const float MIN_MAGNUS_DIRECTION_SQUARED = 0.0001f;
    internal const float MIN_DRAG_VELOCITY = 0.001f;

    // ネットへの反射後に、壁から離す距離。
    internal const float NET_EPSILON = 0.001f;
}
