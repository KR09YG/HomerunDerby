using UnityEngine;
using TMPro;

public class ScoreCalculator : MonoBehaviour
{
    [SerializeField] private BattingBallMove _battingBallMove;
    [SerializeField] private OnAtBatResetEvent _onAtBatResetEvent;
    [SerializeField] private OnBattingResultEvent _onBattingResultEvent;
    [SerializeField] private OnBallLandedEvent _onBallLandedEvent;
    [SerializeField] private TextMeshProUGUI _scoreText;
    private const float ScoreRoundingFactor = 100f;
    private Vector3 _startPos;
    public float ScoreDistance => _scoreDistance;
    private float _scoreDistance;

    private float _totalScore;
    public float TotalScore => _totalScore;
    private bool _isBallLanded;
    private void Awake()
    {
        if (_onAtBatResetEvent) _onAtBatResetEvent.RegisterListener(OnAtBatReset);
        else Debug.LogError("[ScoreCalculator] OnAtBatResetEventが設定されていません。");
        if (_onBattingResultEvent) _onBattingResultEvent.RegisterListener(OnBattingResult);
        else Debug.LogError("[ScoreCalculator] OnBattingResultEventが設定されていません。");
        if (_onBallLandedEvent) _onBallLandedEvent.RegisterListener(OnBallLanded);
        else Debug.LogError("[ScoreCalculator] OnBallLandedEventが設定されていません。");
    }

    private void OnDestroy()
    {
        _onAtBatResetEvent?.UnregisterListener(OnAtBatReset);
        _onBattingResultEvent?.UnregisterListener(OnBattingResult);
        _onBallLandedEvent?.UnregisterListener(OnBallLanded);
    }

    private void OnBattingResult(BattingBallResult result)
    {
        if (result.BallType != BattingBallType.Miss)
        {
            Initialize(_battingBallMove.Position);
        }
    }

    private void OnBallLanded() => _isBallLanded = true;

    private void Update()
    {
        if (_battingBallMove && _battingBallMove.IsMoving && !_isBallLanded)
        {
            CalculateDistance(_battingBallMove.Position);
        }
        else if (!_battingBallMove)
        {
            Debug.LogError("[ScoreCalculator] BattingBallMoveが設定されていません。");
        }
    }

    private void OnAtBatReset()
    {
        _scoreText.text = "";
    }

    public void Initialize(Vector3 startPos)
    {
        _startPos = startPos;
        _isBallLanded = false;
        _scoreDistance = 0;
    }

    public void CalculateDistance(Vector3 ballPos)
    {
        float xDistance = Mathf.Abs(ballPos.x - _startPos.x);
        float zDistance = Mathf.Abs(ballPos.z - _startPos.z);
        // 球場の縮尺を戻して、水平距離をm単位で表示する。
        _scoreDistance = BaseballWorldScale.ToMeters(Mathf.Sqrt(xDistance * xDistance + zDistance * zDistance));
        _scoreText.text = $"{_scoreDistance:F2} m";
    }

    public ResultDisplayData CalculateScore(int consecutiveHomeRunCount, bool isFoul, bool isMiss)
    {
        float scoreDistance = 0f;
        if (!isFoul && !isMiss)
        {
            scoreDistance = Mathf.Round(_scoreDistance * ScoreRoundingFactor) / ScoreRoundingFactor;
        }
        else
        {
            scoreDistance = 0f;
        }

        float finalScore;
        if (consecutiveHomeRunCount > 1)
        {
            finalScore = scoreDistance * consecutiveHomeRunCount;
        }
        else
        {
            finalScore = scoreDistance;
        }

        _totalScore += finalScore;
        Debug.Log($"[ScoreCalculator] Distance: {scoreDistance}, ConsecutiveHomeRunCount: {consecutiveHomeRunCount}, IsFoul: {isFoul}, FinalScore: {finalScore}, TotalScore: {_totalScore}");
        return new ResultDisplayData
        {
            Distance = scoreDistance,
            ConsecutiveHomeRunCount = consecutiveHomeRunCount,
            IsFoul = isFoul,
            FinalScore = finalScore,
            TotalScore = _totalScore
        };
    }
}