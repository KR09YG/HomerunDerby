using TMPro;
using UnityEngine;

[CreateAssetMenu(menuName = "WorldTour/UI Theme")]
public sealed class WorldTourUITheme : ScriptableObject
{
    public Texture2D WorldMap;
    public Texture2D StadiumMap;
    public TMP_FontAsset JapaneseFont;
    public TMP_FontAsset DisplayFont;
    public static WorldTourUITheme Load() => Resources.Load<WorldTourUITheme>("WorldTourUI/Theme");
}
