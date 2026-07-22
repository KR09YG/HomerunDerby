using UnityEngine;

/// <summary>
/// バウンド（地面/壁）の物理パラメータ
/// </summary>
[CreateAssetMenu(fileName = "BounceSettings", menuName = "Baseball/Bounce Settings")]
public class BounceSettings : ScriptableObject
{
    [Header("地面バウンド設定")]
    [Tooltip("地面の高さ (m)")]
    public float groundLevel = 0f;

    [Tooltip("地面の反発係数（0~1）。野球場: 0.4~0.6")]
    [Range(0f, 1f)]
    public float groundRestitution = 0.5f;

    [Tooltip("地面の摩擦係数（0~1）。芝生: 0.3~0.5")]
    [Range(0f, 1f)]
    public float groundFriction = 0.4f;

    [Tooltip("最大バウンド回数")]
    public int maxBounces = 5;

    [Tooltip("バウンド停止速度 (m/s)。この速度以下で停止")]
    public float stopVelocityThreshold = 1f;

    [Header("壁/フェンス設定")]
    [Tooltip("壁バウンドを有効化")]
    public bool enableWallBounce = true;

    [Tooltip("壁の反発係数")]
    [Range(0f, 1f)]
    public float wallRestitution = 0.3f;

    [Tooltip("フィールド境界（ワールド座標）")]
    public Bounds fieldBounds = new Bounds(Vector3.zero, new Vector3(100f, 50f, 100f));

    [Header("転がり設定")]
    [Tooltip("地面で転がる場合の減速率")]
    [Range(0f, 1f)]
    public float rollingDeceleration = 0.95f;

    public float ballRadius = 0.00366f;
}