using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.Networking;

public static class WorldTourConfigClient
{
    public static async UniTask<WorldTourConfig> DownloadAsync(string url, int timeoutSeconds, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new System.IO.InvalidDataException("設定の取得先にはHTTPSのURLが必要です。");
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = Math.Max(1, timeoutSeconds);
            await request.SendWebRequest().ToUniTask(cancellationToken: cancellationToken, cancelImmediately: true);
            cancellationToken.ThrowIfCancellationRequested();
            return WorldTourConfigJson.Parse(request.downloadHandler.text);
        }
    }
}
