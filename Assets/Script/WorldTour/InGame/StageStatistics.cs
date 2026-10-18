using System;

[Serializable]
public class StageStatistics
{
    public int HomeRunCount;
    public float MaxHomeRunDistance;
    // フェアゾーン内に飛んだ打球の総距離
    public float TotalFairDistance;

    //　各方向へのホームラン数
    public int LeftHomeRunCount;
    public int CenterHomeRunCount;
    public int RightHomeRunCount;
}