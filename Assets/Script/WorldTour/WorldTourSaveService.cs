using Cysharp.Threading.Tasks;
using UnityEngine;
using System.Threading;

public class WorldTourSaveService
{
    private static bool _isSaving;

    /// <summary>
    /// すでにセーブデータが存在するかどうかを確認する
    /// </summary>
    /// <returns></returns>
    public static bool HasCredentials()
    {
        return TokenStorage.LoadCredentials() != null;
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

        TokenStorage.SaveCredentials(data.playerId, data.token);

        return true;
    }

    /// <summary>
    /// セーブデータをロードする
    /// </summary>
    /// <returns></returns>
    public static async UniTask<PlayerSaveData> LoadData()
    {
        TokenStorage.Credentials credentials = TokenStorage.LoadCredentials();
        if (credentials == null)
        {
            Debug.LogError("No credentials found. Please register first.");
            return null;
        }
        var saveDataDto = await WorldTourApiClient.LoadSaveData(credentials.PlayerId, credentials.Token);
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
        TokenStorage.Credentials credentials = TokenStorage.LoadCredentials();
        if (credentials == null)
        {
            Debug.LogError("No credentials found. Please register first.");
            return false;
        }
        _isSaving = true;
        try
        {
            PlayerSaveDataDto dto = PlayerSaveDataConverter.ToDto(saveData);
            bool success = await WorldTourApiClient.SavePlayerData(credentials.PlayerId, credentials.Token, dto, cancellationToken);
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
