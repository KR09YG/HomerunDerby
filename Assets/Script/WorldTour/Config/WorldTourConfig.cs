using System;
using System.Collections.Generic;

[Serializable]
public class WorldTourConfig
{
    public int ConfigVersion = 0;

    public List<WorldData> Worlds = new List<WorldData>();

    public int NormalStarReward = 100;
    public int BossStarReward = 300;

    /// <summary>指定した地域に所属するワールドを、設定順に返す。開放状態では絞り込まない。</summary>
    public IReadOnlyList<WorldData> GetWorldsByRegion(string regionId)
    {
        var worlds = new List<WorldData>();
        if (!IsValidRegionId(regionId) || Worlds == null) return worlds;
        foreach (WorldData world in Worlds)
            if (world != null && world.RegionId == regionId) worlds.Add(world);
        return worlds;
    }

    public static bool IsValidRegionId(string regionId)
    {
        switch (regionId)
        {
            case "north_america":
            case "south_america":
            case "europe":
            case "asia":
            case "africa":
            case "oceania":
                return true;
            default:
                return false;
        }
    }

    /// <summary>地域設定の不備を通知する。移行中も既存の進行を止めないよう、設定値は書き換えない。</summary>
    public bool ValidateRegions(Action<string> report = null)
    {
        bool valid = true;
        if (Worlds == null) return true;
        foreach (WorldData world in Worlds)
        {
            if (world == null) continue;
            if (IsValidRegionId(world.RegionId)) continue;
            valid = false;
            report?.Invoke($"RegionIdが未設定または不正です: World={world.WorldId}, RegionId={world.RegionId ?? "null"}");
        }
        return valid;
    }
}
