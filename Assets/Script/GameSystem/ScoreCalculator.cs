using UnityEngine;
using TMPro;

public class ScoreCalculator : MonoBehaviour
{
    [SerializeField] private OnAtBatResetEvent _onAtBatResetEvent;
    [SerializeField] private TextMeshProUGUI _scoreText;
    private const float ScoreRoundingFactor = 100f;
    private Vector3 _startPos;
    public float ScoreDistance => _scoreDistance;
    private float _scoreDistance;

    private float _totalScore;
    public float TotalScore => _totalScore;

    private void Awake()
    {
        if (_onAtBatResetEvent) _onAtBatResetEvent.RegisterListener(OnAtBatReset);
    }

    private void OnDestroy()
    {
        _onAtBatResetEvent?.UnregisterListener(OnAtBatReset);
    }

    private void OnAtBatReset()
    {
        _scoreText.text = "";
    }

    public void Initialize(Vector3 startPos)
    {
        _startPos = startPos;
        _scoreDistance = 0;
    }

    public void CalculateDistance(Vector3 ballPos)
    {
        float xDistance = Mathf.Abs(ballPos.x - _startPos.x);
        float zDistance = Mathf.Abs(ballPos.z - _startPos.z);
        // スコアの距離を計算
        _scoreDistance = Mathf.Sqrt(xDistance * xDistance + zDistance * zDistance);
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