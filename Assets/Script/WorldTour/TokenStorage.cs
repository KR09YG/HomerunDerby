using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;


public static class TokenStorage
{
    private static string FilePath =>
        Path.Combine(Application.persistentDataPath, "token.dat");


    public static void SaveToken(string token)
    {
        byte[] plainBytes = Encoding.UTF8.GetBytes(token);

        byte[] encryptedBytes = ProtectedData.Protect(
            plainBytes,
            null,
            DataProtectionScope.CurrentUser);

        Directory.CreateDirectory(Application.persistentDataPath);
        File.WriteAllBytes(FilePath, encryptedBytes);
    }

    public static string LoadToken()
    {
        if (!File.Exists(FilePath))
            return null;

        byte[] encryptedBytes = File.ReadAllBytes(FilePath);

        byte[] plainBytes = ProtectedData.Unprotect(
            encryptedBytes,
            null,
            DataProtectionScope.CurrentUser);

        return Encoding.UTF8.GetString(plainBytes);
    }
}