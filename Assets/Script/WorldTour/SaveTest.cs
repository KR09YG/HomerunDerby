using UnityEngine;

public class SaveTest : MonoBehaviour
{
    private string jsonString;

    public void SaveData(PlayerSaveDataDto SaveDto)
    {
        // json形式に変換
        jsonString = JsonUtility.ToJson(SaveDto);
    }

    public PlayerSaveDataDto LoadData()
    {
        PlayerSaveDataDto SaveDto = JsonUtility.FromJson<PlayerSaveDataDto>(jsonString);
        return SaveDto;
    }
}
