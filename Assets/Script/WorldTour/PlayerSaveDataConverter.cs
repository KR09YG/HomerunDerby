using System.Collections.Generic;

public static class PlayerSaveDataConverter
{
    public static PlayerSaveDataDto ToDto(PlayerSaveData saveData)
    {
        if (saveData == null) return null;

        PlayerSaveDataDto dto = new PlayerSaveDataDto();
        dto.SchemaVersion = saveData.SchemaVersion;
        dto.Revision = saveData.Revision;
        dto.Points = saveData.Points;
        dto.Stages = new List<StageProgressDataDto>();

        if (saveData.Stages == null) return dto;
        foreach (var kvp in saveData.Stages)
        {
            if (string.IsNullOrEmpty(kvp.Key) || kvp.Value == null) continue;
            StageProgressDataDto stageDto = new StageProgressDataDto();
            stageDto.StageId = kvp.Key;
            stageDto.IsCleared = kvp.Value.IsCleared;
            stageDto.EarnedStars = CopyStars(kvp.Value.EarnedStars);
            dto.Stages.Add(stageDto);
        }

        return dto;
    }

    public static PlayerSaveData FromDto(PlayerSaveDataDto dto)
    {
        if (dto == null) return null;
        PlayerSaveData saveData = new PlayerSaveData();
        saveData.SchemaVersion = dto.SchemaVersion;
        saveData.Revision = dto.Revision;
        saveData.Points = dto.Points;
        if (dto.Stages == null) return saveData;
        foreach (var stageDto in dto.Stages)
        {
            if (stageDto == null || string.IsNullOrEmpty(stageDto.StageId)) continue;
            if (!saveData.Stages.TryGetValue(stageDto.StageId, out StageProgressData stageData))
                stageData = new StageProgressData();
            stageData.IsCleared |= stageDto.IsCleared;
            bool[] stars = CopyStars(stageDto.EarnedStars);
            for (int i = 0; i < stars.Length; i++) stageData.EarnedStars[i] |= stars[i];
            saveData.Stages[stageDto.StageId] = stageData;
        }
        return saveData;
    }

    private static bool[] CopyStars(bool[] source)
    {
        var stars = new bool[3];
        if (source != null) System.Array.Copy(source, stars, System.Math.Min(source.Length, stars.Length));
        return stars;
    }
}
