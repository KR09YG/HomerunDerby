using System;
using System.Collections.Generic;

[Serializable]
public class WorldData
{
    public string WorldId;
    public string RegionId;
    public string Name;
    public string Description;
    public bool IsUnlocked;

    public List<StageData> Stages = new List<StageData>();
    public string NextWorld;
}

