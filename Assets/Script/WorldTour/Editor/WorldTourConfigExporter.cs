using System;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

internal static class WorldTourConfigExporter
{
    private const string FileName = "config.json";

    [MenuItem("Tools/World Tour/Export Config JSON")]
    private static void ExportSelected()
    {
        WorldTourConfigAsset asset = Selection.activeObject as WorldTourConfigAsset;
        if (asset == null)
        {
            EditorUtility.DisplayDialog("World Tour設定の出力", "ProjectでWorldTourConfigAssetを選択してください。", "閉じる");
            return;
        }

        string folder = EditorUtility.SaveFolderPanel(
            "config.jsonの保存先を選択", Path.GetDirectoryName(Application.dataPath), "");
        if (string.IsNullOrEmpty(folder)) return;
        string path = Path.Combine(folder, FileName);
        if (File.Exists(path) && !EditorUtility.DisplayDialog(
                "上書き確認", $"既存のファイルを上書きしますか？\n{path}", "上書きする", "キャンセル")) return;

        try
        {
            asset.Config?.ValidateRegions(message => Debug.LogWarning("[WorldTour Export] " + message, asset));
            ExportToFile(asset, path);
            Debug.Log($"[WorldTour Export] 設定JSONを出力しました: {path}", asset);
            EditorUtility.DisplayDialog("出力完了", $"UTF-8で出力しました。\n{path}", "閉じる");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, asset);
            EditorUtility.DisplayDialog("設定JSONの出力に失敗しました", exception.Message, "閉じる");
        }
    }

    /// <summary>復元時に設定が欠落しないことを確認してから、UTF-8でファイルへ保存する。</summary>
    internal static void ExportToFile(WorldTourConfigAsset asset, string path)
    {
        string json = BuildValidatedJson(asset);
        if (string.IsNullOrEmpty(path)) throw new ArgumentException("保存先が指定されていません。", nameof(path));

        // 検証や書き込みに失敗しても、既存のconfig.jsonを途中までの内容で置き換えない。
        string temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporaryPath, json, new UTF8Encoding(false, true));
            if (File.Exists(path)) File.Replace(temporaryPath, path, null);
            else File.Move(temporaryPath, path);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    internal static string BuildValidatedJson(WorldTourConfigAsset asset)
    {
        if (asset == null || asset.Config == null)
            throw new InvalidOperationException("出力するWorld Tour設定がありません。");

        string json = asset.ToJson();
        WorldTourConfig restored = WorldTourConfigJson.Parse(json);
        if (restored == null)
            throw new InvalidOperationException("出力JSONをWorldTourConfigとして復元できません。");

        JToken expected = JToken.FromObject(asset.Config);
        JToken actual = JToken.FromObject(restored);
        if (!JToken.DeepEquals(expected, actual))
            throw new InvalidOperationException(
                "JSONの復元後に設定の差が検出されたため、出力を中止しました。" +
                "JsonUtilityが対応していない型や設定値が含まれていないか確認してください。");
        return json;
    }
}

/// <summary>Editor側に残っている認証情報を、ビルド前に共通ファイルへ移行する。</summary>
public sealed class WorldTourCredentialMigration : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    [InitializeOnLoadMethod]
    private static void OnScriptsReloaded()
    {
        EditorApplication.delayCall += TryMigrate;
    }

    [MenuItem("Tools/World Tour/Migrate Credentials")]
    private static void TryMigrate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        try
        {
            if (TokenStorage.LoadCredentials() != null)
                Debug.Log("[WorldTour Auth] 共通ファイルから認証情報を読み込めました。");
            else
                Debug.Log("[WorldTour Auth] 既存の認証情報はありません。ゲーム起動時に新規登録します。");
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[WorldTour Auth] 認証情報を移行できませんでした。{exception.Message}");
        }
    }

    public void OnPreprocessBuild(BuildReport report)
    {
        try
        {
            // EditorのPlayerPrefsを参照できるうちに、既存アカウントを移行する。
            TokenStorage.LoadCredentials();
        }
        catch (Exception exception)
        {
            throw new BuildFailedException($"World Tourの認証情報を移行できませんでした。既存の進捗を保護するためビルドを中止します。{exception.Message}");
        }
    }
}
