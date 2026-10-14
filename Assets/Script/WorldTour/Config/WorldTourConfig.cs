using System;
using System.Collections.Generic;

[Serializable]
public class WorldTourConfig
{
    public int ConfigVersion = 0;

    public List<WorldData> Worlds = new List<WorldData>();

    public int NormalStarReward = 100;
    public int BossStarReward = 300;
}