using System;
using System.Collections.Generic;
using UnityEngine;
using Homerunderby;

[Serializable]
public class PitchCatalogEntry
{
    public string PitchId;
    public PitchTypeData PitchData;
}

[CreateAssetMenu(
    fileName = "PitchCatalog",
    menuName = "WorldTour/Pitch Catalog")]
public class PitchCatalog : ScriptableObject
{
    [SerializeField]
    private List<PitchCatalogEntry> _entries = new();

    public bool TryGetPitch(
        string pitchId,
        out PitchTypeData pitchData)
    {
        foreach (var entry in _entries)
        {
            if (entry != null && entry.PitchId == pitchId)
            {
                pitchData = entry.PitchData;
                return pitchData != null;
            }
        }

        pitchData = null;
        return false;
    }
}