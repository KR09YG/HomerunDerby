using Cysharp.Threading.Tasks;
using UnityEngine;
using System.Threading;

public class WorldTourSaveService
{
    private const string PlayerIdKey = "playerId";
    private static bool _isSaving;

    /// <summary>
    /// すでにセーブデータが存在するかどうかを確認する
    /// </summary>
    /// <returns></returns>
    public static bool HasCredentials()
    {
        var token = TokenStorage.LoadToken();
        var playerId = PlayerPrefs.GetString(PlayerIdKey, null);
        // トークンとプレイヤーIDの両方が存在する場合、セーブデータは有効とみなす
        return !string.IsNullOrEmpty(token) && !string.IsNullOrEmpty(playerId);
    }

    /// <summary>
    /// プレイヤーの情報を登録する
    /// </summary>
    /// <returns></returns>
    public static async UniTask<bool> RegisterData()
    {
        var data = await WorldTourApiClient.CreatePlayerAsync();

        if (data == null
            || string.IsNullOrEmpty(data.playerId)
            || string.IsNullOrEmpty(data.token))
        {
            return false;
        }

        TokenStorage.SaveToken(data.token);
        PlayerPrefs.SetString(PlayerIdKey, data.playerId);
        PlayerPrefs.Save();

        return true;
    }

    /// <summary>
    /// セーブデータをロードする
    /// </summary>
    /// <returns></returns>
    public static async UniTask<PlayerSaveData> LoadData()
    {
        if (!HasCredentials())
        {
            Debug.LogError("No credentials found. Please register first.");
            return null;
        }
        var playerId = PlayerPrefs.GetString(PlayerIdKey, null);
        var token = TokenStorage.LoadToken();
        var saveDataDto = await WorldTourApiClient.LoadSaveData(playerId, token);
        return PlayerSaveDataConverter.FromDto(saveDataDto);
    }

    /// <summary>
    /// セーブデータを保存する
    /// </summary>
    public static async UniTask<bool> SaveData(PlayerSaveData saveData, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_isSaving)
        {
            Debug.LogWarning("[WorldTour] 保存処理がすでに実行されています。");
            return false;
        }
        if (saveData == null)
        {
            Debug.LogError("Save data is null.");
            return false;
        }
        if (!HasCredentials())
        {
            Debug.LogError("No credentials found. Please register first.");
            return false;
        }
        _isSaving = true;
        try
        {
            PlayerSaveDataDto dto = PlayerSaveDataConverter.ToDto(saveData);
            var playerId = PlayerPrefs.GetString(PlayerIdKey, null);
            var token = TokenStorage.LoadToken();
            bool success = await WorldTourApiClient.SavePlayerData(playerId, token, dto, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (success)
                saveData.Revision = dto.Revision;
            return success;
        }
        finally
        {
            _isSaving = false;
        }
    }
}
