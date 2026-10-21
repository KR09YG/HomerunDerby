using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

/// <summary>画面内でURLごとの画像を共有し、現在選択中の表示先にだけ反映する。</summary>
[DisallowMultipleComponent]
public sealed class WorldTourImageLoader : MonoBehaviour
{
    private sealed class ImageBinding
    {
        public Texture Fallback;
        public Rect FallbackUv;
        public string Url;
        public int Revision;
    }

    private const int TimeoutSeconds = 15;
    private readonly Dictionary<RawImage, ImageBinding> _bindings = new Dictionary<RawImage, ImageBinding>();
    private readonly Dictionary<string, Task<Texture2D>> _downloads = new Dictionary<string, Task<Texture2D>>();
    private readonly HashSet<Texture2D> _textures = new HashSet<Texture2D>();
    private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();

    public void SetImage(RawImage image, string url)
    {
        if (image == null) return;
        if (!_bindings.TryGetValue(image, out ImageBinding binding))
        {
            binding = new ImageBinding { Fallback = image.texture, FallbackUv = image.uvRect };
            _bindings.Add(image, binding);
        }
        // 同じ画像を表示中・読み込み中なら、選択枠だけの更新で再取得しない。
        if (binding.Url == url && !string.IsNullOrEmpty(url) && _downloads.ContainsKey(url)) return;
        binding.Url = url;
        int revision = ++binding.Revision;
        image.texture = binding.Fallback;
        image.uvRect = binding.FallbackUv;
        if (string.IsNullOrWhiteSpace(url)) return;
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            Debug.LogWarning($"[WorldTour Image] HTTPSの画像URLを指定してください: {url}", this);
            return;
        }
        ApplyAsync(image, binding, revision, url, _lifetime.Token).Forget();
    }

    private async UniTask ApplyAsync(RawImage image, ImageBinding binding, int revision, string url, CancellationToken cancellationToken)
    {
        try
        {
            if (!_downloads.TryGetValue(url, out Task<Texture2D> download))
            {
                // 背景とカードから同時に待てるよう、通信1件につきTaskを共有する。
                download = DownloadAsync(url, cancellationToken).AsTask();
                _downloads.Add(url, download);
            }
            Texture2D texture = await download;
            cancellationToken.ThrowIfCancellationRequested();
            if (image == null || binding.Revision != revision) return;
            TourUI.SetTexture(image, texture);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception exception)
        {
            _downloads.Remove(url);
            // 画像だけの通信失敗で、ステージ選択やゲーム進行は止めない。
            if (image != null && binding.Revision == revision)
                Debug.LogWarning($"[WorldTour Image] 画像を取得できませんでした: {url}\n{exception.Message}", this);
        }
    }

    private async UniTask<Texture2D> DownloadAsync(string url, CancellationToken cancellationToken)
    {
        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url, true))
        {
            request.timeout = TimeoutSeconds;
            await request.SendWebRequest().ToUniTask(cancellationToken: cancellationToken, cancelImmediately: true);
            cancellationToken.ThrowIfCancellationRequested();
            Texture2D texture = DownloadHandlerTexture.GetContent(request);
            if (texture == null) throw new InvalidOperationException("取得したデータを画像として読み込めません。");
            _textures.Add(texture);
            return texture;
        }
    }

    private void OnDestroy()
    {
        _lifetime.Cancel();
        foreach (Texture2D texture in _textures)
            if (texture != null)
            {
                if (Application.isPlaying) Destroy(texture);
                else DestroyImmediate(texture);
            }
        _textures.Clear();
        _downloads.Clear();
        _bindings.Clear();
        _lifetime.Dispose();
    }
}
