using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

[CreateAssetMenu(menuName = "WorldTour/Config Editor")]
public class WorldTourConfigAsset : ScriptableObject
{
    public WorldTourConfig Config;

    public string ToJson()
    {
        return JsonConvert.SerializeObject(
            Config,
            Formatting.Indented,
            new StringEnumConverter()
        );
    }
}