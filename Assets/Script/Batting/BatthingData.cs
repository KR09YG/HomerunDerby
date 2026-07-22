using UnityEngine;

[CreateAssetMenu(fileName = "BattingParameters", menuName = "Baseball/Batting Parameters")]
public class BattingData : ScriptableObject
{
    [Header("バット物理パラメータ")]
    [Tooltip("バットの質量 (kg)")]
    public float batMass = 0.9f;

    [Tooltip("バットのスイング速度 (km/h)")]
    public float batSpeedKmh = 150f;

    [Tooltip("反発係数")]
    [Range(0.4f, 0.65f)]
    public float coefficientOfRestitution = 0.55f;

    [Header("インパクト判定パラメータ")]
    [Tooltip("芯の判定半径 (m)")]
    public float sweetSpotRadius = 0.05f;

    [Tooltip("最大インパクト距離 (m)")]
    public float maxImpactDistance = 0.15f;

    [Header("回転パラメータ")]
    [Tooltip("基準バックスピン量 (RPM)")]
    [Range(1000f, 3500f)]
    public float baseBackspinRPM = 2500f;

    [Tooltip("スピン効率")]
    [Range(0f, 1f)]
    public float spinEfficiency = 0.8f;

    [Header("タイミング判定パラメータ")]
    [Tooltip("ファール判定の閾値（0.0-1.0）")]
    [Range(0.5f, 0.9f)]
    public float foulThreshold = 0.7f;

    [Tooltip("フェアゾーンの最大水平角度（度）")]
    [Range(30f, 60f)]
    public float maxFairAngle = 45f;

    [Tooltip("ファールゾーンの最大水平角度（度）")]
    [Range(60f, 90f)]
    public float maxFoulAngle = 80f;

    [Header("打ち上げ角度パラメータ")]
    [Tooltip("理想的な打ち上げ角度（度）- ジャストミート時の角度")]
    [Range(10f, 30f)]
    public float idealLaunchAngle = 20f;

    [Tooltip("角度の累乗指数 - 高いほど大きなズレに厳しい")]
    [Range(1.0f, 2.5f)]
    public float launchAnglePower = 1.5f;

    [Tooltip("角度スケール - 高いほど角度変化が大きい")]
    [Range(10f, 25f)]
    public float launchAngleScale = 15f;

    [Tooltip("最小打ち上げ角度（度）")]
    [Range(-20f, 0f)]
    public float minLaunchAngle = -10f;

    [Tooltip("最大打ち上げ角度（度）")]
    [Range(40f, 60f)]
    public float maxLaunchAngle = 50f;
}