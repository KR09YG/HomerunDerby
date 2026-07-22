using Cysharp.Threading.Tasks;
using KanKikuchi.AudioManager;
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
    private BattingBallResult _currentResult;
    private CancellationTokenSource _cts;
    private int _consecutiveHomeRunCount = 0;
    private int _homeRunCount = 0;
    private bool _isResultDisplaying = false;

    private void Awake()
    {
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
        Debug.Log("[Manager] ボール到着　結果表示へ");
        if (_batterAnimationControl.IsSwinging)
        {
            Debug.Log("[Manager] スイング中のため、スイング終了待ち");
            WaitSwinging().Forget();
        }
        else
        {
            ResultDisplay().Forget();
        }
    }

    private void OnBallLanded()
    {
        ResultDisplay().Forget();
    }

    private async UniTaskVoid WaitSwinging()
    {
        await UniTask.WaitUntil(() => _batterAnimationControl.IsFinSwing);
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
            _isResultDisplaying = true;
        }

        _atBatResetEvent?.RaiseEvent();
        // リザルト表示
        _cts = new CancellationTokenSource();
        await _resultDisplay.DisplayResult(resultData, _cts.Token);
        _isResultDisplaying = false;
        _cts = null;
        // クリック待ち
        await UniTask.WaitUntil(() => Input.GetMouseButtonDown(0));
        _resultDisplay.ResultHide();
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
