using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using TMPro;
using UnityEngine;
using DG.Tweening;

public class CountdownDisplay : MonoBehaviour
{
    private const int CountdownStart = 3;
    [SerializeField] private int _startDelayMs = 1000;
    [SerializeField] private float _pulseDuration = 0.5f;
    [SerializeField] private float _scaleFactor = 1.5f;
    [SerializeField] private TextMeshProUGUI _countdownText;
    public async UniTask ShowRemainingBalls(int count, float durationSeconds, CancellationToken cancellationToken)
    {
        TMP_FontAsset originalFont = _countdownText.font;
        float originalSize = _countdownText.fontSize;
        Vector2 originalBounds = _countdownText.rectTransform.sizeDelta;
        TextAlignmentOptions originalAlignment = _countdownText.alignment;
        WorldTourUITheme theme = WorldTourUITheme.Load();
        try
        {
            if (theme != null) _countdownText.font = theme.JapaneseFont;
            _countdownText.enabled = true;
            _countdownText.fontSize = 42f;
            _countdownText.rectTransform.sizeDelta = new Vector2(460f, 80f);
            _countdownText.alignment = TextAlignmentOptions.Center;
            _countdownText.transform.localScale = Vector3.one;
            _countdownText.text = $"残り {count} 球";
            await UniTask.Delay(TimeSpan.FromSeconds(durationSeconds), ignoreTimeScale: true,
                cancellationToken: cancellationToken);
        }
        finally
        {
            if (_countdownText != null)
            {
                _countdownText.text = "";
                _countdownText.font = originalFont;
                _countdownText.fontSize = originalSize;
                _countdownText.rectTransform.sizeDelta = originalBounds;
                _countdownText.alignment = originalAlignment;
            }
        }
    }

    public async UniTask PlayCountdown(Action action, CancellationToken cancellationToken = default)
    {
        try
        {
            await UniTask.Delay(_startDelayMs, cancellationToken: cancellationToken);
            _countdownText.enabled = true;
            for (int number = CountdownStart; number > 0; number--)
            {
                _countdownText.text = number.ToString();
                await _countdownText.transform.DOScale(_scaleFactor, _pulseDuration)
                    .SetEase(Ease.OutBack).ToUniTask(cancellationToken: cancellationToken);
                await _countdownText.transform.DOScale(1f, _pulseDuration)
                    .SetEase(Ease.InBack).ToUniTask(cancellationToken: cancellationToken);
            }
            _countdownText.text = "GO!";
            await _countdownText.transform.DOScale(_scaleFactor, _pulseDuration).
                SetEase(Ease.OutBack).ToUniTask(cancellationToken: cancellationToken);
            action?.Invoke();
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (_countdownText != null)
            {
                _countdownText.text = "";
                _countdownText.transform.localScale = Vector3.one;
            }
        }
    }
}
