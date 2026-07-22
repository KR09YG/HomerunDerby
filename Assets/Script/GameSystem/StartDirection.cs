using Cysharp.Threading.Tasks;
using System;
using TMPro;
using UnityEngine;
using DG.Tweening;

public class StartDirection : MonoBehaviour
{
    [SerializeField] private int _delayTime = 1000;
    [SerializeField] private float _animationTime = 0.5f;
    [SerializeField] private float _scaleFactor = 1.5f;
    [SerializeField] private TextMeshProUGUI _directionText;
    public async UniTaskVoid Direction(Action action)
    {
        await UniTask.Delay(_delayTime);
        _directionText.enabled = true;
        _directionText.text = "3";
        // テキストを大きくするアニメーション
        await _directionText.transform.DOScale(_scaleFactor, _animationTime).
            SetEase(Ease.OutBack).AsyncWaitForCompletion();
        // テキストを元の大きさに戻すアニメーション
        await _directionText.transform.DOScale(1f, _animationTime).
            SetEase(Ease.InBack).AsyncWaitForCompletion();
        _directionText.text = "2";
        await _directionText.transform.DOScale(_scaleFactor, _animationTime).
            SetEase(Ease.OutBack).AsyncWaitForCompletion();
        await _directionText.transform.DOScale(1f, _animationTime).
            SetEase(Ease.InBack).AsyncWaitForCompletion();
        _directionText.text = "1";
        await _directionText.transform.DOScale(_scaleFactor, _animationTime).
            SetEase(Ease.OutBack).AsyncWaitForCompletion();
        await _directionText.transform.DOScale(1f, _animationTime).
            SetEase(Ease.InBack).AsyncWaitForCompletion();
        _directionText.text = "GO!";
        await _directionText.transform.DOScale(_scaleFactor, _animationTime).
            SetEase(Ease.OutBack).AsyncWaitForCompletion();
        action?.Invoke();
        _directionText.text = "";
        _directionText.transform.DOScale(1f, 0f); // スケールを元に戻す
    }
}
