using System.Collections;
using UnityEngine;
public static class JsonTest 
{
    public static WorldTourConfig LoadConfig(string json)
    {
        WorldTourConfig config = JsonUtility.FromJson<WorldTourConfig>(json);
        config?.ValidateRegions(message => Debug.LogWarning("[WorldTour] " + message));
        return config;
    }
}
