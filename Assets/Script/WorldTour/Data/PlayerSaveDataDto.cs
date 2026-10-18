using System;
using System.Collections.Generic;

/// <summary>
/// ネット上に保存するためのクラス
/// PlayerSaveDataをこれに変換して保存する
/// </summary>
[Serializable]
public class PlayerSaveDataDto
{
    public int StageId;
    public int SchemaVersion;
    public int Revision;
    public int Points;

    public List<StageProgressDataDto> Stages;
}

[Serializable] 
public class StageProgressDataDto
{
    public string StageId;
    public bool IsCleared;
    public bool[] EarnedStars = new bool[3];
}