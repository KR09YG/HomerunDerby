using Cysharp.Threading.Tasks;
using System;
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
    public async UniTaskVoid PlayCountdown(Action action)
    {
        await UniTask.Delay(_startDelayMs);
        _countdownText.enabled = true;
        for (int number = CountdownStart; number > 0; number--)
        {
            _countdownText.text = number.ToString();
            await _countdownText.transform.DOScale(_scaleFactor, _pulseDuration).
                SetEase(Ease.OutBack).AsyncWaitForCompletion();
            await _countdownText.transform.DOScale(1f, _pulseDuration).
                SetEase(Ease.InBack).AsyncWaitForCompletion();
        }
        _countdownText.text = "GO!";
        await _countdownText.transform.DOScale(_scaleFactor, _pulseDuration).
            SetEase(Ease.OutBack).AsyncWaitForCompletion();
        action?.Invoke();
        _countdownText.text = "";
        _countdownText.transform.DOScale(1f, 0f); // スケールを元に戻す
    }
}
