using System.Collections;
using UnityEngine;
public static class JsonTest 
{
    public static WorldTourConfig LoadConfig(string json)
    {
        return JsonUtility.FromJson<WorldTourConfig>(json);
    }
}
