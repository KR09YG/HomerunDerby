using System;
using System.Collections.Generic;

/// <summary>
/// ネット上に保存するためのクラス
/// PlayerSaveDataをこれに変換して保存する
/// </summary>
[Serializable]
public class PlayerSaveDataDto
{
    public int SchemaVersion;
    public int Revision;
    public int Points;

    public Dictionary<string, StageProgressData> Stages = new();
}
