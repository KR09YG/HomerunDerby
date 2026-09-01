using Cysharp.Threading.Tasks;
using KanKikuchi.AudioManager;
using System.Linq;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HomeRunDerbyManager : MonoBehaviour
{
    [SerializeField] private OnStartPitchEvent _startPitchEvent;
    [SerializeField] private OnAtBatResetEvent _atBatResetEvent;
    [SerializeField] private OnBallReachedTargetEvent _ballReachedTargetEvent;
    [SerializeField] private OnBattingResultEvent _battingResultEvent;
    [SerializeField] private OnBallLandedEvent _ballLandedEvent;

    [SerializeField] private BatterAnimationControl _batterAnimationControl;
    [SerializeField] private StartDirection _startDirection;
    [SerializeField] private ScoreCalculator _scoreCalculator;
    [SerializeField] private ResultDisplay _resultDisplay;
    [SerializeField] private TextMeshProUGUI _ballCountText;
    [SerializeField] private Button _restartButton;
    [SerializeField] private int _ballCount = 10;
    [SerializeField] private float _resultDisplayDelay = 1f; // 結果表示までの待機時間(s)
    [SerializeField] private float _fadeDuration = 0.5f; // フェードの時間(s)
    private BattingBallResult _currentResult;
    private CancellationTokenSource _cts;
    private int _consecutiveHomeRunCount = 0;
    private int _homeRunCount = 0;
    private bool _isResultDisplaying = false;

    private void Awake()
    {
        _ballCountText.text = $"×{_ballCount}";
        if (_battingResultEvent != null) _battingResultEvent.RegisterListener(OnBattingResultReceived);
        else Debug.LogError("BattingResultEvent が設定されていません");
        if (_ballLandedEvent != null) _ballLandedEvent.RegisterListener(OnBallLanded);
        else Debug.LogError("BallLandedEvent が設定されていません");
        if (_ballReachedTargetEvent != null) _ballReachedTargetEvent.RegisterListener(ResetAtBat);
        else Debug.LogError("BallReachedTargetEvent が設定されていません");
    }

    private void OnDestroy()
    {
        _battingResultEvent?.UnregisterListener(OnBattingResultReceived);
        _ballLandedEvent?.UnregisterListener(OnBallLanded);
        _ballReachedTargetEvent?.UnregisterListener(ResetAtBat);
    }

    private void Start()
    {
        Fade.FadeImage(_fadeDuration, false);
        BGMManager.Instance.Play(
            BGMPath.SPORTS_SEASON, 0.3f, 0, 1, true, false);
        _startDirection.Direction(null).Forget();
    }

    private void OnBattingResultReceived(BattingBallResult result)
    {
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

    private void ResetAtBat(PitchBallMove ball)
    {
        _currentResult = new BattingBallResult(BattingBallType.Miss);
        WaitResultDisplay().Forget();
    }

    private void OnBallLanded()
    {
        if (_currentResult.BallType != BattingBallType.HomeRun)
        {
            WaitResultDisplay().Forget();
        }
        else
        {
            ResultDisplay().Forget();
        }
    }

    private async UniTaskVoid WaitResultDisplay()
    {
        await UniTask.WaitForSeconds(_resultDisplayDelay);
        ResultDisplay().Forget();
    }

    private async UniTaskVoid ResultDisplay()
    {
        ResultDisplayData resultData;
        if (_currentResult == null)
        {
            // 結果がない場合は見逃し
            resultData = _scoreCalculator.CalculateScore(_consecutiveHomeRunCount, false, true);
        }
        else
        {
            // ファウル判定
            bool isFoul = _currentResult.BallType == BattingBallType.Foul;
            // ミス判定
            bool isMiss = _currentResult.BallType == BattingBallType.Miss;
            // スコア計算
            resultData = _scoreCalculator.CalculateScore(_consecutiveHomeRunCount, isFoul, isMiss);
        }


        // スコアに加算された時のみ結果表示
        if (_currentResult.BallType == BattingBallType.Hit || _currentResult.BallType == BattingBallType.HomeRun)
        {
            _isResultDisplaying = true;
            // リザルト表示
            _cts = new CancellationTokenSource();
            await _resultDisplay.DisplayResult(resultData, _cts.Token);
            _cts = null;
            _isResultDisplaying = false;
            await UniTask.WhenAny(UniTask.WaitUntil(
                () => Input.GetMouseButtonDown(0)),
                UniTask.WaitForSeconds(_resultDisplayDelay));
        }

        Fade.FadeImage(_fadeDuration, true);
        _atBatResetEvent?.RaiseEvent();
        _resultDisplay.ResultHide();
        await UniTask.WaitForSeconds(_fadeDuration);
        Fade.FadeImage(_fadeDuration, false);
        StartNextAtBat();
    }

    private void StartNextAtBat()
    {
        _ballCount--;
        if (_ballCount <= 0)
        {
            Debug.Log("全ての打席が終了しました");
            _resultDisplay.DisPlayFinalResult(_homeRunCount, ShowRestartButton).Forget();
            return;
        }
        Debug.Log("次の打席を開始");
        _ballCountText.text = $"×{_ballCount}";
        _currentResult = null;
        StartPitch();
    }

    private void ShowRestartButton()
    {
        Cursor.visible = true;
        _restartButton.gameObject.SetActive(true);
    }

    public void Restart()
    {
        // Sceneをリロードしてゲームを再開する
        UnityEngine.SceneManagement.
            SceneManager.LoadScene(UnityEngine.SceneManagement.
            SceneManager.GetActiveScene().name);
    }

    private void StartPitch()
    {
        Debug.Log("投球を開始");
        _startPitchEvent.RaiseEvent();

    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (_isResultDisplaying)
            {
                _cts?.Cancel();
            }
        }
    }
}
