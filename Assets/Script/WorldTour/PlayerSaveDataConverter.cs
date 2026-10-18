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

        foreach (var kvp in saveData.Stages)
        {
            StageProgressDataDto stageDto = new StageProgressDataDto();
            stageDto.StageId = kvp.Key;
            stageDto.IsCleared = kvp.Value.IsCleared;
            stageDto.EarnedStars = (bool[])kvp.Value.EarnedStars.Clone();
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
        foreach (var stageDto in dto.Stages)
        {
            StageProgressData stageData = new StageProgressData();
            stageData.IsCleared = stageDto.IsCleared;
            stageData.EarnedStars = (bool[])stageDto.EarnedStars.Clone();
            saveData.Stages[stageDto.StageId] = stageData;
        }
        return saveData;
    }
}
