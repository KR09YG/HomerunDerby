using System;
using Cysharp.Threading.Tasks;
using System.Threading;
using TMPro;
using UnityEngine;
using DG.Tweening;

public struct ResultDisplayData
{
    public BattingBallType BallType;
    public float Distance;
    public int ConsecutiveHomeRunCount;
    public bool IsFoul;
    public float FinalScore;
    public float TotalScore;
}

public class ResultDisplay : MonoBehaviour
{
    [Header("結果表示")]
    [SerializeField] private TextMeshProUGUI _ballType;
    [SerializeField] private GameObject _resultPanel;
    [SerializeField] private GameObject _distancePanel;
    [SerializeField] private TextMeshProUGUI _distanceText;
    [SerializeField] private GameObject _bonusPanel;
    [SerializeField] private TextMeshProUGUI _bonusText;
    [SerializeField] private GameObject _finalScorePanel;
    [SerializeField] private TextMeshProUGUI _finalScoreText;
    [SerializeField] private GameObject _totalScorePanel;
    [SerializeField] private TextMeshProUGUI _totalScoreText;
    [Header("最終スコア表示")]
    [SerializeField] private GameObject _finalResultPanel;
    [SerializeField] private TextMeshProUGUI _homerunCountText;
    [SerializeField] private TextMeshProUGUI _finalTotalScoreText;
    [Tooltip("各カウントアップアニメーションの時間(秒)")]
    [SerializeField] private float _countUpDuration = 1.0f;
    [Tooltip("次の項目表示までの待機時間(ms)")]
    [SerializeField] private int _toNextResult = 500;

    private const int BonusThreshold = 2;

    public void ResultHide()
    {
        _resultPanel.SetActive(false);
    }

    // LayoutGroupを維持するため、パネルではなく子テキストの描画を切り替える。
    private void ResetDisplay()
    {
        _distanceText.text = "0";
        _bonusText.text = "0";
        _finalScoreText.text = "0";
        _totalScoreText.text = "0";

        SetTextVisible(_distancePanel, false);
        SetTextVisible(_bonusPanel, false);
        SetTextVisible(_finalScorePanel, false);
        SetTextVisible(_totalScorePanel, false);
    }

    private void SetTextVisible(GameObject panel, bool visible)
    {
        if (panel == null) return;

        var texts = panel.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (var t in texts)
        {
            t.enabled = visible;
        }
    }

    public async UniTask DisplayResult(ResultDisplayData data, CancellationToken cancellationToken = default)
    {
        _resultPanel.SetActive(true);
        ResetDisplay();

        if (_ballType != null) _ballType.text = data.BallType.ToString();

        bool showBonus = data.ConsecutiveHomeRunCount >= BonusThreshold;

        try
        {
            SetTextVisible(_distancePanel, true);
            await CountUpAsync(_distanceText, data.Distance, cancellationToken);
            await UniTask.Delay(_toNextResult, cancellationToken: cancellationToken);

            if (showBonus)
            {
                SetTextVisible(_bonusPanel, true);
                await CountUpAsync(_bonusText, data.ConsecutiveHomeRunCount, cancellationToken);
                await UniTask.Delay(_toNextResult, cancellationToken: cancellationToken);
            }

            SetTextVisible(_finalScorePanel, true);
            await CountUpAsync(_finalScoreText, data.FinalScore, cancellationToken);
            await UniTask.Delay(_toNextResult, cancellationToken: cancellationToken);

            SetTextVisible(_totalScorePanel, true);
            await CountUpAsync(_totalScoreText, data.TotalScore, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // 演出を飛ばした場合も、表示値は最終スコアに揃える。
            ShowFinalValues(data, showBonus);
        }
    }

    private async UniTask CountUpAsync(
        TextMeshProUGUI text,
        float targetValue,
        CancellationToken cancellationToken,
        string suffix = "")
    {
        float value = 0f;
        Tween tween = DOTween.To(
            () => value,
            x =>
            {
                value = x;
                text.text = Mathf.FloorToInt(value).ToString() + suffix;
            },
            targetValue,
            _countUpDuration
            ).SetEase(Ease.OutCubic);

        try
        {
            await tween.ToUniTask(cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // キャンセル後にTweenがテキストを更新しないよう停止する。
            tween.Kill();
            text.text = Mathf.FloorToInt(targetValue).ToString() + suffix;
            throw;
        }
    }

    private void ShowFinalValues(ResultDisplayData data, bool showBonus)
    {
        SetTextVisible(_distancePanel, true);
        SetTextVisible(_finalScorePanel, true);
        SetTextVisible(_totalScorePanel, true);

        _distanceText.text = Mathf.FloorToInt(data.Distance).ToString();
        _finalScoreText.text = Mathf.FloorToInt(data.FinalScore).ToString();
        _totalScoreText.text = Mathf.FloorToInt(data.TotalScore).ToString();

        if (showBonus)
        {
            SetTextVisible(_bonusPanel, true);
            _bonusText.text = Mathf.FloorToInt(data.ConsecutiveHomeRunCount).ToString();
        }
    }

    public async UniTaskVoid DisplayFinalResult(int homerunCount, float totalScore, Action action = null)
    {
        _finalResultPanel.SetActive(true);
        await CountUpAsync(_homerunCountText, homerunCount, CancellationToken.None);
        await CountUpAsync(_finalTotalScoreText, totalScore, CancellationToken.None);
        action?.Invoke();
    }
}