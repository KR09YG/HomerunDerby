using System;
using UnityEngine;

[Serializable]
public struct BallData
{
    public string Name;
    [Tooltip("リリース時の球速（km/h）")]
    public float Speed;
    [Tooltip("総回転数（rpm）。回転方向はSpin Axisで指定する。")]
    public float RotateSpeed;
    [Tooltip("ワールド座標の回転軸。計算時に正規化する。+Zへ投げる場合、-Xがバックスピン、-Yが-X側へ曲がる回転。")]
    public Vector3 SpinAxis;
}
