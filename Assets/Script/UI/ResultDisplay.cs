using System;
using Cysharp.Threading.Tasks;
using System.Threading;
using TMPro;
using UnityEngine;
using DG.Tweening;

public struct ResultDisplayData
{
    public float Distance;
    public int ConsecutiveHomeRunCount;
    public bool IsFoul;
    public float FinalScore;
    public float TotalScore;
}

public class ResultDisplay : MonoBehaviour
{
    [Header("結果表示")]
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

    // ボーナスを表示する最小の連続ホームラン数
    private const int BonusThreshold = 2;

    /// <summary>
    /// 結果を非表示にする
    /// </summary>
    public void ResultHide()
    {
        _resultPanel.SetActive(false);
    }

    /// <summary>
    /// 表示前のリセット。LayoutGroupのレイアウト崩れを避けるため、
    /// SetActiveではなくTextの描画(enabled)で表示/非表示を制御する
    /// </summary>
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

    /// <summary>
    /// パネルの子にあるTextMeshProUGUIの描画をまとめて切り替える
    /// </summary>
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

        bool showBonus = data.ConsecutiveHomeRunCount >= BonusThreshold;

        try
        {
            // 距離
            SetTextVisible(_distancePanel, true);
            await CountUpAsync(_distanceText, data.Distance, cancellationToken);
            await UniTask.Delay(_toNextResult, cancellationToken: cancellationToken);

            // 連続ホームランボーナス(2以上のときのみ)
            if (showBonus)
            {
                SetTextVisible(_bonusPanel, true);
                await CountUpAsync(_bonusText, data.ConsecutiveHomeRunCount, cancellationToken);
                await UniTask.Delay(_toNextResult, cancellationToken: cancellationToken);
            }

            // 今回のスコア
            SetTextVisible(_finalScorePanel, true);
            await CountUpAsync(_finalScoreText, data.FinalScore, cancellationToken);
            await UniTask.Delay(_toNextResult, cancellationToken: cancellationToken);

            // 合計スコア
            SetTextVisible(_totalScorePanel, true);
            await CountUpAsync(_totalScoreText, data.TotalScore, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // キャンセルされたら全項目を最終値で即表示
            ShowFinalValues(data, showBonus);
        }
    }

    /// <summary>
    /// テキストの数値を0から目標値までアニメーションをする
    /// </summary>
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
            // このTweenを止めて最終値に確定してから例外を再送出
            tween.Kill();
            text.text = Mathf.FloorToInt(targetValue).ToString() + suffix;
            throw;
        }
    }

    /// <summary>
    /// 全項目を最終値で即座に表示
    /// </summary>
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

    public async UniTaskVoid DisPlayFinalResult(int homerunCount, Action action)
    {
        _finalResultPanel.SetActive(true);
        await CountUpAsync(_homerunCountText, homerunCount, CancellationToken.None);
        await CountUpAsync(_finalTotalScoreText, float.Parse(_totalScoreText.text), CancellationToken.None);
        action?.Invoke();
    }
}