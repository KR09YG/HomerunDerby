using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;


public static class TokenStorage
{
    [Serializable]
    public sealed class Credentials
    {
        public string PlayerId;
        public string Token;
    }

    private const string LegacyPlayerIdKey = "playerId";
    private static string FilePath => Path.Combine(Application.persistentDataPath, "credentials.dat");
    private static string LegacyTokenPath => Path.Combine(Application.persistentDataPath, "token.dat");

    /// <summary>プレイヤーIDとトークンをまとめて暗号化し、保存する。</summary>
    public static void SaveCredentials(string playerId, string token)
    {
        if (string.IsNullOrWhiteSpace(playerId) || string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("プレイヤーIDとトークンの両方が必要です。");

        var credentials = new Credentials { PlayerId = playerId, Token = token };
        byte[] encryptedBytes = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(JsonUtility.ToJson(credentials)), null, DataProtectionScope.CurrentUser);

        Directory.CreateDirectory(Application.persistentDataPath);
        string temporaryPath = FilePath + ".tmp";
        try
        {
            File.WriteAllBytes(temporaryPath, encryptedBytes);
            if (File.Exists(FilePath)) File.Replace(temporaryPath, FilePath, null);
            else File.Move(temporaryPath, FilePath);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    /// <summary>認証情報を復元する。旧形式は両方の情報が揃っている場合だけ移行する。</summary>
    public static Credentials LoadCredentials()
    {
        if (File.Exists(FilePath))
        {
            byte[] plainBytes = ProtectedData.Unprotect(
                File.ReadAllBytes(FilePath), null, DataProtectionScope.CurrentUser);
            Credentials credentials = JsonUtility.FromJson<Credentials>(Encoding.UTF8.GetString(plainBytes));
            if (credentials == null || string.IsNullOrWhiteSpace(credentials.PlayerId) ||
                string.IsNullOrWhiteSpace(credentials.Token))
                throw new InvalidDataException("保存された認証情報にプレイヤーIDまたはトークンがありません。");
            return credentials;
        }

        string playerId = PlayerPrefs.GetString(LegacyPlayerIdKey, null);
        bool hasToken = File.Exists(LegacyTokenPath);
        if (string.IsNullOrEmpty(playerId) && !hasToken) return null;
        if (string.IsNullOrEmpty(playerId) || !hasToken)
            throw new InvalidDataException("旧形式の認証情報が不完全です。Editorで登録した場合は、Editorの「Tools → World Tour → Migrate Credentials」で移行してから起動してください。");

        byte[] tokenBytes = ProtectedData.Unprotect(
            File.ReadAllBytes(LegacyTokenPath), null, DataProtectionScope.CurrentUser);
        string token = Encoding.UTF8.GetString(tokenBytes);
        SaveCredentials(playerId, token);
        // 新形式の保存が完了するまでは、移行元を削除しない。
        PlayerPrefs.DeleteKey(LegacyPlayerIdKey);
        PlayerPrefs.Save();
        File.Delete(LegacyTokenPath);
        Debug.Log("[WorldTour Auth] 認証情報を共通の暗号化ファイルへ移行しました。");
        return new Credentials { PlayerId = playerId, Token = token };
    }
}
