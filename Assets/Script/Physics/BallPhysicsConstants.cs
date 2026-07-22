using UnityEngine;

internal static class BallPhysicsConstants
{
    // === 物理定数 ===
    internal const float AIR_DENSITY = 1.225f;
    internal const float BALL_MASS = 0.145f;
    internal const float BALL_RADIUS = 0.0366f;
    internal const float CROSS_SECTION = Mathf.PI * BALL_RADIUS * BALL_RADIUS;
    internal const float DRAG_COEFFICIENT = 0.3f;
    internal const float GRAVITY_HALF = 0.5f;
    internal const float MAGNUS_FORCE_HALF = 0.5f;
    internal const float DRAG_FORCE_HALF = 0.5f;

    // === 単位変換定数 ===
    internal const float RPM_TO_RAD_PER_SEC = 2f * Mathf.PI / 60f;

    // === マグヌス効果補正 ===
    internal const float MAGNUS_VERTICAL_CORRECTION_FACTOR = 0.8f;

    // === 最適化パラメータ ===
    internal const int MAX_OPTIMIZATION_ITERATIONS = 20;
    internal const float POSITION_TOLERANCE = 0.02f;
    internal const float Z_POSITION_TOLERANCE = 0.01f;
    internal const float Z_TOLERANCE_FACTOR = 0.5f;
    internal const float Z_ERROR_WEIGHT = 2f;

    // === 速度調整パラメータ ===
    internal const float Z_ADJUSTMENT_INITIAL = 0.8f;
    internal const float Z_ADJUSTMENT_FINAL = 0.3f;
    internal const float XY_ADJUSTMENT_INITIAL = 0.6f;
    internal const float XY_ADJUSTMENT_FINAL = 0.2f;
    internal const float SPEED_ERROR_THRESHOLD = 0.2f;
    internal const float SPEED_ADJUSTMENT_INITIAL = 0.15f;
    internal const float SPEED_ADJUSTMENT_FINAL = 0.05f;

    // === 初速推定パラメータ ===
    internal const float DRAG_FACTOR_BASE = 1.0f;
    internal const float DRAG_MASS_FACTOR = 2f;

    // === シミュレーション終了条件 ===
    internal const float GROUND_LEVEL = -0.5f;
    internal const int MAX_TRAJECTORY_POINTS = 10000;

    // === 物理計算の閾値 ===
    internal const float MIN_VELOCITY_SQUARED = 0.01f;
    internal const float MIN_MAGNUS_DIRECTION_SQUARED = 0.0001f;
    internal const float MIN_DRAG_VELOCITY = 0.001f;

    // === Net反射：壁に貼り付くのを防ぐ微小押し戻し ===
    internal const float NET_EPSILON = 0.001f;
}
