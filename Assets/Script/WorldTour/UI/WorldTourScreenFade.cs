using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>読み込み中は画面と操作を覆い、準備ができてから表示する。</summary>
public sealed class WorldTourScreenFade : MonoBehaviour
{
    private CanvasGroup _group;
    private TMP_Text _message;
    private Tween _tween;
    private UniTaskCompletionSource _fadeCompletion;
    private int _revision;

    public static WorldTourScreenFade Create(Transform parent, string message)
    {
        RectTransform root = TourUI.Canvas(parent, "World Tour Screen Fade", 32700);
        root.GetComponent<Canvas>().overrideSorting = true;
        var view = root.gameObject.AddComponent<WorldTourScreenFade>();
        view._group = root.gameObject.AddComponent<CanvasGroup>();
        Image background = TourUI.Rect(root, "Black", 0, 0, 1920, 1080).gameObject.AddComponent<Image>();
        background.rectTransform.anchorMin = Vector2.zero;
        background.rectTransform.anchorMax = Vector2.one;
        background.rectTransform.offsetMin = background.rectTransform.offsetMax = Vector2.zero;
        background.color = Color.black;
        background.raycastTarget = true;
        WorldTourUITheme theme = WorldTourUITheme.Load();
        view._message = TourUI.Text(root, "Loading Message", message, 360, 500, 1200, 80, 32,
            theme != null ? theme.JapaneseFont : null, Color.white, true);
        view.Cover(message);
        return view;
    }

    public void Cover(string message)
    {
        _revision++;
        StopFade();
        _group.alpha = 1f;
        _group.blocksRaycasts = true;
        _message.gameObject.SetActive(true);
        _message.text = message;
    }

    public async UniTask HideAsync(float duration, string message, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _revision++;
        StopFade();
        _group.blocksRaycasts = true;
        _message.gameObject.SetActive(true);
        _message.text = message;
        await FadeAlphaAsync(1f, duration, _revision, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
    }

    public async UniTask RevealAsync(float duration, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        int revision = ++_revision;
        StopFade();
        _message.gameObject.SetActive(false);
        await FadeAlphaAsync(0f, duration, revision, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (this != null && revision == _revision) _group.blocksRaycasts = false;
    }

    private async UniTask FadeAlphaAsync(float target, float duration, int revision, CancellationToken cancellationToken)
    {
        var completion = new UniTaskCompletionSource();
        _fadeCompletion = completion;
        // 切り替え前のTweenが残っても、新しい画面の透明度は変更させない。
        Tween tween = DOTween.To(() => _group.alpha, alpha =>
        {
            if (this != null && revision == _revision) _group.alpha = alpha;
        }, target, duration).SetUpdate(true)
            .OnComplete(() => completion.TrySetResult())
            .OnKill(() => completion.TrySetResult());
        _tween = tween;
        using (cancellationToken.Register(() =>
        {
            tween.Kill();
            completion.TrySetCanceled(cancellationToken);
        }))
        {
            await completion.Task;
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    private void StopFade()
    {
        Tween tween = _tween;
        UniTaskCompletionSource completion = _fadeCompletion;
        _tween = null;
        _fadeCompletion = null;
        tween?.Kill();
        // Tweenの停止通知が遅れても、切り替え前の待機を残さない。
        completion?.TrySetResult();
    }

    private void OnDestroy()
    {
        _revision++;
        StopFade();
    }
}
