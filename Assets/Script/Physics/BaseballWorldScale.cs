using UnityEngine;

public static class BaseballWorldScale
{
    // 球場の投手板22.14単位 ≒ 18.44m。塁間と外野フェンスでも約1.2倍になる。
    public const float UnitsPerMeter = 1.2f;

    public static float ToMeters(float worldUnits) => worldUnits / UnitsPerMeter;
    public static Vector2 ToMeters(Vector2 worldUnits) => worldUnits / UnitsPerMeter;
    public static Vector3 ToMeters(Vector3 worldUnits) => worldUnits / UnitsPerMeter;
    public static float ToWorldUnits(float meters) => meters * UnitsPerMeter;
    public static Vector3 ToWorldUnits(Vector3 meters) => meters * UnitsPerMeter;
}
