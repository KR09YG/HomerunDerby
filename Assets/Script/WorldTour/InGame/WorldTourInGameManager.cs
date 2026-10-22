using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using KanKikuchi.AudioManager;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WorldTourInGameManager : MonoBehaviour
{
    private const float FoulResolutionTimeoutSeconds = 6f;
    private const float StageSaveTimeoutSeconds = 15f;
    private enum FlowState
    {
        Uninitialized,
        WaitingForStartAndStage,
        WaitingForStart,
        WaitingForStage,
        CountingDown,
        PreparingPitch,
        Pitching,
        ResultQueued,
        ResultCommitted,
        ResultDisplaying,
        AwaitingNextAtBat,
        Transitioning,
        Finished,
        ShuttingDown
    }

    [Header("Events")]
    [SerializeField] private OnStartPitchEvent _startPitchEvent;
    [SerializeField] private OnAtBatResetEvent _atBatResetEvent;
    [SerializeField] private OnBallReachedTargetEvent _ballReachedTargetEvent;
    [SerializeField] private OnBattingResultEvent _battingResultEvent;
    [SerializeField] private OnBallLandedEvent _ballLandedEvent;

    [Header("Components")]
    [SerializeField] private CountdownDisplay _countdownDisplay;
    [SerializeField] private ResultTallyer _resultTallyer;
    [SerializeField] private GameObject _resultPanel;
    [SerializeField] private TextMeshProUGUI _resultText;
    [SerializeField] private TMP_FontAsset _stageResultFont;

    [Header("Timing")]
    [SerializeField] private float _resultDisplayDelay = 1f;
    [SerializeField] private float _fadeDuration = 0.5f;
    [SerializeField, Min(0f)] private float _remainingBallNoticeSeconds = 1.2f;

    [Header("Audio")]
    [SerializeField] private float _bgmVolume = 0.3f;
    [SerializeField] private float _delayBGMStartTime;

    private BattingBallResult _currentResult;
    private CancellationTokenSource _lifetimeCts;
    private CancellationTokenSource _displayCts;
    private StageSessionController _stageSessionController;
    private FlowState _state;
    private StageData _currentStage;
    private PlayerSaveData _saveData;
    private enum StageSaveState { NotStarted, Saving, Failed, Saved }
    private StageSaveState _stageSaveState;
    private WorldTourStageResultView.SaveControls _stageResultView;
    private WorldTourScreenFade _screenFade;

    public event Action<BattingBallResult> AtBatCompleted;

    public bool IsAwaitingNextAtBat => _state == FlowState.AwaitingNextAtBat;
    public StageSessionData Session => _stageSessionController?.Session;

    private void Awake()
    {
        _lifetimeCts = new CancellationTokenSource();
        _screenFade = WorldTourScreenFade.Create(transform, "準備中…");
        if (_startPitchEvent == null || _atBatResetEvent == null ||
            _ballReachedTargetEvent == null || _battingResultEvent == null ||
            _ballLandedEvent == null || _countdownDisplay == null || _resultTallyer == null)
        {
            Debug.LogError("[WorldTourInGameManager] イベントまたは必須コンポーネントの参照が未設定です。", this);
            enabled = false;
            return;
        }

        _state = FlowState.WaitingForStartAndStage;

        if ((_resultPanel == null) != (_resultText == null))
            Debug.LogWarning("[WorldTourInGameManager] 結果表示にはパネルとテキストの両方が必要です。表示をスキップします。", this);
        else if (_resultPanel != null)
            _resultPanel.SetActive(false);

        if (_battingResultEvent != null) _battingResultEvent.RegisterListener(OnBattingResultReceived);
        else Debug.LogError("[WorldTourInGameManager] BattingResultEventが未設定です。");
        if (_ballLandedEvent != null) _ballLandedEvent.RegisterListener(OnBallLanded);
        else Debug.LogError("[WorldTourInGameManager] BallLandedEventが未設定です。");
        if (_ballReachedTargetEvent != null) _ballReachedTargetEvent.RegisterListener(OnPitchReachedTarget);
        else Debug.LogError("[WorldTourInGameManager] BallReachedTargetEventが未設定です。");

        _stageSessionController = new StageSessionController();
    }

    private void Start()
    {
        if (Fade.instance != null) Fade.FadeImage(0f, false);
        BGMManager.Instance.Play(
            BGMPath.SPORTS_SEASON, _bgmVolume, _delayBGMStartTime, 1, true, false);

        if (_state == FlowState.WaitingForStart)
            StartCountdown();
        else if (_state == FlowState.WaitingForStartAndStage)
            _state = FlowState.WaitingForStage;
    }

    private void OnDestroy()
    {
        bool hasPendingSave = _state == FlowState.Finished && _stageSaveState != StageSaveState.Saved;
        _state = FlowState.ShuttingDown;
        _displayCts?.Cancel();
        _lifetimeCts?.Cancel();
        _battingResultEvent?.UnregisterListener(OnBattingResultReceived);
        _ballLandedEvent?.UnregisterListener(OnBallLanded);
        _ballReachedTargetEvent?.UnregisterListener(OnPitchReachedTarget);
        _lifetimeCts?.Dispose();
        if (hasPendingSave) WorldTourManager.Instance?.SetStageSavePending(false);
    }

    private void OnApplicationQuit()
    {
        _state = FlowState.ShuttingDown;
        _lifetimeCts?.Cancel();
    }

    public void BeginStage(StageData stageData, PlayerSaveData saveData)
    {
        if (!isActiveAndEnabled ||
            (_state != FlowState.WaitingForStartAndStage && _state != FlowState.WaitingForStage)) return;
        if (stageData == null)
        {
            Debug.LogError("[WorldTourInGameManager] ステージ情報が未設定です。", this);
            return;
        }
        if (saveData == null)
        {
            Debug.LogError("[WorldTourInGameManager] プレイヤーの進捗データが未設定です。", this);
            return;
        }
        StageStatistics stageStatistics = _stageSessionController.Initialize(stageData);
        _resultTallyer.Initialize(stageStatistics);
        _currentStage = stageData;
        _saveData = saveData;

        EnsureResultUI();

        if (stageData.BallCount <= 0)
        {
            _stageSessionController.ShouldFinishStage();
            FinishStage();
        }
        else if (_state == FlowState.WaitingForStartAndStage)
        {
            _state = FlowState.WaitingForStart;
        }
        else
        {
            StartCountdown();
        }
    }

    private void StartCountdown()
    {
        if (_state != FlowState.WaitingForStart && _state != FlowState.WaitingForStage) return;

        _state = FlowState.CountingDown;
        StartCountdownAsync(_lifetimeCts.Token).Forget();
    }

    private async UniTask StartCountdownAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _screenFade.RevealAsync(_fadeDuration, cancellationToken);
            await _countdownDisplay.PlayCountdown(StartPitch, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }

    private void StartPitch()
    {
        if (_state != FlowState.CountingDown && _state != FlowState.Transitioning) return;
        _state = FlowState.PreparingPitch;
        PreparePitchAsync(_lifetimeCts.Token).Forget();
    }

    private async UniTask PreparePitchAsync(CancellationToken cancellationToken)
    {
        try
        {
            // 開始カウントダウンの後片付けが終わってから、同じ表示欄を使う。
            await UniTask.NextFrame(cancellationToken: cancellationToken);
            await _countdownDisplay.ShowRemainingBalls(Session.RemainingBalls, _remainingBallNoticeSeconds, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (_state != FlowState.PreparingPitch) return;
            _currentResult = null;
            _state = FlowState.Pitching;
            _startPitchEvent.RaiseEvent();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }

    private void OnBattingResultReceived(BattingBallResult result)
    {
        if ((_state != FlowState.Pitching && _state != FlowState.ResultQueued) ||
            _currentResult != null || result == null) return;
        _currentResult = result;
        if (result.BallType == BattingBallType.Foul)
            EnsureFoulProgressAsync(result, _lifetimeCts.Token).Forget();
    }

    private async UniTask EnsureFoulProgressAsync(BattingBallResult result, CancellationToken cancellationToken)
    {
        try
        {
            await UniTask.WaitForSeconds(FoulResolutionTimeoutSeconds, cancellationToken: cancellationToken);
            if (_state != FlowState.Pitching || _currentResult != result) return;

            Debug.LogWarning("[WorldTour] ファールの到達通知が届かなかったため、打席結果を確定します。", this);
            QueueResult(0f);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
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
        if (_state != FlowState.Pitching) return;

        _state = FlowState.ResultQueued;
        ShowAtBatResultAsync(delaySeconds, _lifetimeCts.Token).Forget();
    }

    private async UniTask ShowAtBatResultAsync(float delaySeconds, CancellationToken cancellationToken)
    {
        try
        {
            if (delaySeconds > 0f)
                await UniTask.WaitForSeconds(delaySeconds, cancellationToken: cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            _state = FlowState.ResultCommitted;
            // 見送りでは打撃結果イベントが来ないため、空振りとして扱う。
            _currentResult ??= new BattingBallResult(BattingBallType.Miss);

            await ShowResultAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            _state = FlowState.AwaitingNextAtBat;
            _stageSessionController.CompleteAtBat();
            try
            {
                AtBatCompleted?.Invoke(_currentResult);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }

            if (Session != null && Session.IsFinished)
                FinishStage();
            else
                ContinueToNextAtBat();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
        }
    }

    private async UniTask ShowResultAsync(CancellationToken cancellationToken)
    {
        if (_resultPanel == null || _resultText == null) return;

        _resultText.text = _currentResult.BallType == BattingBallType.Miss
            ? "MISS"
            : $"{_currentResult.BallType.ToString().ToUpperInvariant()}\n{_currentResult.Distance:F1} m";
        _resultPanel.SetActive(true);
        _state = FlowState.ResultDisplaying;

        var displayCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _displayCts = displayCts;
        try
        {
            await UniTask.WaitForSeconds(
                Mathf.Max(0f, _resultDisplayDelay), cancellationToken: displayCts.Token);
        }
        catch (OperationCanceledException) when (
            displayCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            if (_state == FlowState.ResultDisplaying)
                _state = FlowState.ResultCommitted;
            _displayCts = null;
            displayCts.Dispose();
            if (_resultPanel != null) _resultPanel.SetActive(false);
        }
    }

    public void ContinueToNextAtBat()
    {
        if (_state != FlowState.AwaitingNextAtBat) return;

        _state = FlowState.Transitioning;
        ContinueToNextAtBatAsync(_lifetimeCts.Token).Forget();
    }

    private async UniTask ContinueToNextAtBatAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _screenFade.HideAsync(_fadeDuration, "次の投球を準備しています…", cancellationToken);
            if (_state != FlowState.Transitioning) return;
            FindFirstObjectByType<BattingBallMove>()?.StopAndHide();
            _atBatResetEvent.RaiseEvent();
            _currentResult = null;
            await UniTask.NextFrame(cancellationToken: cancellationToken);
            await _screenFade.RevealAsync(_fadeDuration, cancellationToken);
            if (_state == FlowState.Transitioning) StartPitch();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }

    private void FinishStage()
    {
        if (_state == FlowState.Finished || _state == FlowState.ShuttingDown) return;
        _state = FlowState.Finished;
        StageSessionData session = Session;
        if (session == null)
        {
            Debug.LogError("[WorldTourInGameManager] ステージの終了状態を取得できません。", this);
            return;
        }

        Debug.Log($"[WorldTour] StageId={session.StageId} " +
                  $"{(session.IsCleared ? "CLEAR" : "FAILED")} " +
                  $"RemainingBalls={session.RemainingBalls} " +
                  $"HomeRunCount={session.Statistics.HomeRunCount} " +
                  $"Stars=[{session.AchievedStars[0]}, {session.AchievedStars[1]}, {session.AchievedStars[2]}]", this);

        if (_resultPanel != null) _resultPanel.SetActive(false);
        var previousStars = new bool[3];
        if (_saveData.Stages.TryGetValue(session.StageId, out StageProgressData previous) && previous?.EarnedStars != null)
            Array.Copy(previous.EarnedStars, previousStars, Mathf.Min(previous.EarnedStars.Length, previousStars.Length));
        int newStarCount = StageProgressService.MergeStageProgress(_saveData, session);
        WorldTourManager.Instance?.SetStageSavePending(true);
        WorldTourManager.Instance?.SetStageProgress(session, newStarCount);
        _stageResultView = WorldTourStageResultView.Create(
            transform, session, _stageResultFont, newStarCount, previousStars, RetryStageSave);
        RetryStageSave();
    }

    private void RetryStageSave()
    {
        if (_state != FlowState.Finished || _stageSaveState == StageSaveState.Saving ||
            _stageSaveState == StageSaveState.Saved || _lifetimeCts == null || _lifetimeCts.IsCancellationRequested) return;

        _stageSaveState = StageSaveState.Saving;
        _screenFade.Cover("保存中…");
        _stageResultView?.SetSaving();
        SaveStageAsync(_lifetimeCts.Token).Forget();
    }

    private async UniTask SaveStageAsync(CancellationToken cancellationToken)
    {
        try
        {
            // 再試行でも進捗はマージし直さず、同じ共有データを保存する。
            Debug.Log($"[WorldTour] クラウド保存開始 StageId={Session?.StageId} Revision={_saveData.Revision}", this);
            bool saved;
            using (var saveCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                using var timeout = saveCts.CancelAfterSlim(TimeSpan.FromSeconds(StageSaveTimeoutSeconds), DelayType.Realtime);
                try
                {
                    saved = await WorldTourSaveService.SaveData(_saveData, saveCts.Token);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    Debug.LogError("[WorldTour] 保存がタイムアウトしました。リザルトから再試行してください。", this);
                    saved = false;
                }
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (this == null || _state != FlowState.Finished) return;

            if (saved)
                Debug.Log($"[WorldTour] クラウド保存完了 StageId={Session?.StageId} Revision={_saveData.Revision}", this);
            else
                Debug.LogWarning($"[WorldTour] クラウド保存失敗 StageId={Session?.StageId}", this);

            if (_stageResultView != null && !_stageResultView.AreImagesReady)
            {
                _screenFade.Cover("画像を読み込み中…");
                await UniTask.WaitUntil(() => _stageResultView.AreImagesReady, cancellationToken: cancellationToken);
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (this == null || _state != FlowState.Finished) return;

            if (saved)
            {
                _stageResultView?.SetSaved();
            }
            else
            {
                _stageResultView?.SetSaveFailed();
            }
            await _screenFade.RevealAsync(_fadeDuration, cancellationToken);
            if (this == null || _state != FlowState.Finished) return;
            _stageSaveState = saved ? StageSaveState.Saved : StageSaveState.Failed;
            if (saved) WorldTourManager.Instance?.SetStageSavePending(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            if (cancellationToken.IsCancellationRequested || this == null || _state != FlowState.Finished) return;
            _stageSaveState = StageSaveState.Failed;
            _stageResultView?.SetSaveFailed();
            await _screenFade.RevealAsync(_fadeDuration, cancellationToken);
            Debug.LogException(exception, this);
        }
    }

    private void EnsureResultUI()
    {
        if (_resultPanel != null && _resultText != null) return;

        var canvasObject = new GameObject("World Tour Result UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        _resultPanel = new GameObject("Result Panel", typeof(RectTransform), typeof(Image));
        _resultPanel.transform.SetParent(canvasObject.transform, false);
        RectTransform panelRect = (RectTransform)_resultPanel.transform;
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(760f, 210f);
        panelRect.anchoredPosition = new Vector2(0f, 240f);
        _resultPanel.GetComponent<Image>().color = new Color(0.03f, 0.07f, 0.12f, 0.88f);

        var textObject = new GameObject("Result Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(_resultPanel.transform, false);
        RectTransform textRect = (RectTransform)textObject.transform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(16f, 12f);
        textRect.offsetMax = new Vector2(-16f, -12f);
        _resultText = textObject.GetComponent<TextMeshProUGUI>();
        _resultText.alignment = TextAlignmentOptions.Center;
        _resultText.fontSize = 38f;
        _resultText.color = Color.white;
        _resultPanel.SetActive(false);
    }

    private void Update()
    {
        if (_state == FlowState.ResultDisplaying && Input.GetMouseButtonDown(0))
            _displayCts?.Cancel();
    }
}
