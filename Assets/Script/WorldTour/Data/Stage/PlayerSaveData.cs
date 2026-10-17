using System;
using System.Collections.Generic;

[Serializable]
public class PlayerSaveData
{
    // セーブデータの製造バージョン
    public int SchemaVersion = 1;
    // セーブデータの更新番号
    public int Revision = 0;
    // 所持ポイント
    public int Points = 0;

    // ステージの進行状況
    public Dictionary<string, StageProgressData> Stages = new();
}