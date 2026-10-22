using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public static class WorldTourSaveService
{
    private static bool _isSaving;

    /// <summary>
    /// ローカルにプレイヤーIDとトークンが揃っているか確認する。
    /// </summary>
    public static bool HasCredentials()
    {
        return TokenStorage.LoadCredentials() != null;
    }

    /// <summary>
    /// プレイヤーを登録し、発行された認証情報を保存する。
    /// </summary>
    public static async UniTask<bool> RegisterDataAsync()
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
    /// クラウドからプレイヤーの進捗を読み込む。
    /// </summary>
    public static async UniTask<PlayerSaveData> LoadDataAsync()
    {
        TokenStorage.Credentials credentials = TokenStorage.LoadCredentials();
        if (credentials == null)
        {
            Debug.LogError("No credentials found. Please register first.");
            return null;
        }
        var saveDataDto = await WorldTourApiClient.LoadSaveDataAsync(credentials.PlayerId, credentials.Token);
        return PlayerSaveDataConverter.FromDto(saveDataDto);
    }

    /// <summary>
    /// クラウドへ進捗を保存し、成功時はサーバーのRevisionを反映する。
    /// </summary>
    public static async UniTask<bool> SaveDataAsync(PlayerSaveData saveData, CancellationToken cancellationToken = default)
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
            bool success = await WorldTourApiClient.SavePlayerDataAsync(credentials.PlayerId, credentials.Token, dto, cancellationToken);
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
