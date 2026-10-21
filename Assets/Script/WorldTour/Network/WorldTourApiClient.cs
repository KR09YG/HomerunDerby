using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;

public static class WorldTourApiClient
{
    private const string BASE_URL = "https://y9noekispf.execute-api.ap-southeast-2.amazonaws.com";

    [Serializable]
    public class PlayerData
    {
        public string playerId;
        public string token;
        public PlayerSaveDataDto save;
    }

    public static async UniTask<PlayerData> CreatePlayerAsync()
    {
        using var request = UnityWebRequest.PostWwwForm($"{BASE_URL}/register", UnityWebRequest.kHttpVerbPOST);
        request.timeout = 10;

        request.downloadHandler = new DownloadHandlerBuffer();
        try
        {
            await request.SendWebRequest().ToUniTask();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Failed to create player: {request.error}");
                return null;
            }

            return JsonUtility.FromJson<PlayerData>(request.downloadHandler.text);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Exception occurred while creating player: {ex.Message}");
            return null;

        }
    }

    [Serializable]
    public class GetSaveResponse
    {
        public string playerId;
        public PlayerSaveDataDto save;
    }

    /// <summary>
    /// セーブデータの取得
    /// </summary>
    public static async UniTask<PlayerSaveDataDto> LoadSaveData(string playerId, string token)
    {
        using var request = UnityWebRequest.Get($"{BASE_URL}/save");

        request.SetRequestHeader("x-player-id", playerId);
        request.SetRequestHeader("Authorization", $"Bearer {token}");

        try
        {
            await request.SendWebRequest().ToUniTask();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Failed to load save data: {request.error}");
                return null;
            }

            GetSaveResponse response = JsonUtility.FromJson<GetSaveResponse>(request.downloadHandler.text);

            if (response == null || response.save == null)
            {
                Debug.LogError("Failed to parse save data from response.");
                return null;
            }

            return response.save;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Exception occurred while loading save data: {ex.Message}");
            return null;
        }
    }

    [Serializable]
    public class PutSaveRequest
    {
        public int expectedRevision;
        public PlayerSaveDataDto save;
    }

    [Serializable]
    public class PutSaveResponse
    {
        public string message;
        public int revision;
        public PlayerSaveDataDto save;
    }

    /// <summary>
    /// セーブデータの保存
    /// </summary>
    public static async UniTask<bool> SavePlayerData(string playerId, string token, PlayerSaveDataDto saveData,
        CancellationToken cancellationToken = default)
    {
        if (saveData == null)
        {
            Debug.LogError("Save data is null.");
            return false;
        }

        var body = new PutSaveRequest
        {
            expectedRevision = saveData.Revision,
            save = saveData
        };

        string jsonData = JsonUtility.ToJson(body);
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);

        using var request = new UnityWebRequest(
            $"{BASE_URL}/save",
            UnityWebRequest.kHttpVerbPUT);

        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();

        request.SetRequestHeader("x-player-id", playerId);
        request.SetRequestHeader("Authorization", $"Bearer {token}");
        request.SetRequestHeader("Content-Type", "application/json");
        request.timeout = 10;

        try
        {
            await request.SendWebRequest().ToUniTask(cancellationToken: cancellationToken, cancelImmediately: true);
            cancellationToken.ThrowIfCancellationRequested();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError(
                    $"Failed to save player data: HTTP {request.responseCode}, {request.error}");
                return false;
            }

            PutSaveResponse response =
                JsonUtility.FromJson<PutSaveResponse>(
                    request.downloadHandler.text);

            if (response == null || response.save == null)
            {
                Debug.LogError("Invalid save response.");
                return false;
            }

            // サーバーで更新されたRevisionを送信元DTOに反映する
            saveData.Revision = response.revision;

            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Debug.LogError(
                $"Exception occurred while saving player data: {ex.Message}");
            return false;
        }
    }
}
