using Cysharp.Threading.Tasks;
using KanKikuchi.AudioManager;
using System;
using System.Threading;
using TMPro;
using UnityEngine;

public class HomeRunDerbyManager : MonoBehaviour
{
    [Header("Events")]
    [SerializeField] private OnStartPitchEvent _startPitchEvent;
    [SerializeField] private OnAtBatResetEvent _atBatResetEvent;
    [SerializeField] private OnBallReachedTargetEvent _ballReachedTargetEvent;
    [SerializeField] private OnBattingResultEvent _battingResultEvent;
    [SerializeField] private OnBallLandedEvent _ballLandedEvent;

    [Header("Components")]
    [SerializeField] private CountdownDisplay _countdownDisplay; // 投球開始のカウントダウン表示
    [SerializeField] private ScoreCalculator _scoreCalculator;
    [SerializeField] private ResultDisplay _resultDisplay;
    [SerializeField] private TextMeshProUGUI _ballCountText;
    [Header("Game Settings")]
    [SerializeField] private int _ballCount = 10;
    [SerializeField] private float _resultDisplayDelay = 1f; // 結果表示までの待機時間(s)
    [SerializeField] private float _fadeDuration = 0.5f; // フェードの時間(s)
    [Header("Audio Settings")]
    [SerializeField] private float _bgmVolume = 0.3f; // BGMの音量
    [SerializeField] private float _delayBGMStartTime = 0f; // BGM再生開始までの遅延時間(s)
    [SerializeField] private float _pitchBGMVolume = 0.3f; // ピッチBGMの音量
    private BattingBallResult _currentResult;
    private CancellationTokenSource _lifetimeCts;
    private CancellationTokenSource _displayCts;
    private int _consecutiveHomeRunCount = 0;
    private int _homeRunCount = 0;
    private bool _isResultDisplaying;
    private bool _pitchRequested;
    private bool _resultQueued;
    private bool _resultCommitted;
    private bool _isShuttingDown;

    private void Awake()
    {
        _lifetimeCts = new CancellationTokenSource();
        if (_startPitchEvent == null || _atBatResetEvent == null ||
            _ballReachedTargetEvent == null || _battingResultEvent == null || _ballLandedEvent == null ||
            _countdownDisplay == null || _scoreCalculator == null || _resultDisplay == null || _ballCountText == null)
        {
            Debug.LogError("[HomeRunDerbyManager] イベントまたは画面表示の参照が未設定です。", this);
            enabled = false;
            return;
        }

        _ballCountText.text = $"×{_ballCount}";
        _battingResultEvent.RegisterListener(OnBattingResultReceived);
        _ballLandedEvent.RegisterListener(OnBallLanded);
        _ballReachedTargetEvent.RegisterListener(OnPitchReachedTarget);
    }

    private void OnDestroy()
    {
        _isShuttingDown = true;
        _lifetimeCts?.Cancel();
        _displayCts?.Cancel();
        _battingResultEvent?.UnregisterListener(OnBattingResultReceived);
        _ballLandedEvent?.UnregisterListener(OnBallLanded);
        _ballReachedTargetEvent?.UnregisterListener(OnPitchReachedTarget);
        _lifetimeCts?.Dispose();
    }

    private void Start()
    {
        Fade.FadeImage(_fadeDuration, false);
        BGMManager.Instance.Play(
            BGMPath.SPORTS_SEASON, _bgmVolume, _delayBGMStartTime, 1, true, false);
        _countdownDisplay.PlayCountdown(StartPitch, _lifetimeCts.Token).Forget();
    }

    private void OnBattingResultReceived(BattingBallResult result)
    {
        if (!_pitchRequested || _resultCommitted || _currentResult != null || result == null) return;
        _currentResult = result;
        if (result.BallType == BattingBallType.HomeRun)
        {
            _consecutiveHomeRunCount++;
            _homeRunCount++;
        }
        else
        {
            _consecutiveHomeRunCount = 0;
        }
    }

    private void OnPitchReachedTarget(PitchBallMove ball)
    {
        if (ball == null) return;
        QueueResult(_resultDisplayDelay);
    }

    private void OnBallLanded()
    {
        if (_currentResult == null || _currentResult.BallType == BattingBallType.Miss) return;
        QueueResult(_currentResult.BallType == BattingBallType.HomeRun ? 0f : _resultDisplayDelay);
    }

    private void QueueResult(float delaySeconds)
    {
        if (_isShuttingDown || !_pitchRequested || _resultQueued) return;
        _resultQueued = true;
        ShowAtBatResultAsync(delaySeconds, _lifetimeCts.Token).Forget();
    }

    private async UniTask ShowAtBatResultAsync(float delaySeconds, CancellationToken cancellationToken)
    {
        try
        {
            if (delaySeconds > 0f)
                await UniTask.WaitForSeconds(delaySeconds, cancellationToken: cancellationToken);

            _resultCommitted = true;
            // 見送りでは打撃結果イベントが来ないため、ここで空振りとして扱う。
            if (_currentResult == null)
            {
                _currentResult = new BattingBallResult(BattingBallType.Miss);
                _consecutiveHomeRunCount = 0;
            }

            bool isFoul = _currentResult.BallType == BattingBallType.Foul;
            bool isMiss = _currentResult.BallType == BattingBallType.Miss;
            ResultDisplayData resultData = _scoreCalculator.CalculateScore(_consecutiveHomeRunCount, isFoul, isMiss);
            resultData.BallType = _currentResult.BallType;

            if (!isFoul && !isMiss)
            {
                _isResultDisplaying = true;
                var displayCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                _displayCts = displayCts;
                try
                {
                    await _resultDisplay.DisplayResult(resultData, displayCts.Token);
                }
                finally
                {
                    _isResultDisplaying = false;
                    _displayCts = null;
                    displayCts.Dispose();
                }

                cancellationToken.ThrowIfCancellationRequested();
                float closeAt = Time.time + Mathf.Max(0f, _resultDisplayDelay);
                await UniTask.WaitUntil(
                    () => Input.GetMouseButtonDown(0) || Time.time >= closeAt,
                    cancellationToken: cancellationToken);
            }

            _resultDisplay.ResultHide();
            await UniTask.WaitForSeconds(_fadeDuration, cancellationToken: cancellationToken);
            StartNextAtBat();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private void StartNextAtBat()
    {
        _pitchRequested = false;
        _ballCount--;
        if (_ballCount <= 0)
        {
            Debug.Log("全ての打席が終了しました");
            _resultDisplay.DisplayFinalResult(_homeRunCount, _scoreCalculator.TotalScore).Forget();
            return;
        }

        Fade.FadeImage(_fadeDuration, true, () =>
        {
            if (_isShuttingDown) return;
            _atBatResetEvent?.RaiseEvent();
            Fade.FadeImage(_fadeDuration, false, () =>
            {
                if (_isShuttingDown) return;
                _ballCountText.text = $"×{_ballCount}";
                StartPitch();
            });
        });
    }

    public void Restart()
    {
        UnityEngine.SceneManagement.
            SceneManager.LoadScene(UnityEngine.SceneManagement.
            SceneManager.GetActiveScene().name);
    }

    private void StartPitch()
    {
        if (_isShuttingDown || _pitchRequested || _ballCount <= 0) return;
        _currentResult = null;
        _resultQueued = false;
        _resultCommitted = false;
        _pitchRequested = true;
        _startPitchEvent.RaiseEvent();
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (_isResultDisplaying)
            {
                _displayCts?.Cancel();
            }
        }
    }
}
