using UnityEngine;

public class ResultTallyer : MonoBehaviour
{
    [SerializeField] OnBattingResultEvent _onBattingResultEvent;
    [SerializeField] BattingDistanceTracker _distanceTracker;
    private StageStatistics _stageStatistics;
    public StageStatistics StageStatistics => _stageStatistics;

    private void Awake()
    {
        if (_onBattingResultEvent) _onBattingResultEvent.RegisterListener(OnBattingResultReceive);
        else Debug.LogError("OnBattingResultEventが設定されていません");
    }

    private void OnDestroy()
    {
        if (_onBattingResultEvent) _onBattingResultEvent.UnregisterListener(OnBattingResultReceive);
    }

    /// <summary>
    /// イベントかマネージャークラスに呼び出してもらう
    /// </summary>
    private void Initialize()
    {
        _stageStatistics = new StageStatistics();
    }

    private void OnBattingResultReceive(BattingBallResult result)
    {
        // ホームランかヒットなら距離を加算する
        if (result.BallType == BattingBallType.HomeRun ||
            result.BallType == BattingBallType.Hit ||
            result.BallType == BattingBallType.GroundBall)
        {
            float distance = _distanceTracker.CalculateDistance(
                result.TrajectoryPoints[result.LandingIndex], result.TrajectoryPoints[0]);
            _stageStatistics.TotalFairDistance += distance;

            // 最大飛距離を更新する
            if (result.BallType == BattingBallType.HomeRun)
            {
                _stageStatistics.HomeRunCount++;
                if (_stageStatistics.MaxHomeRunDistance < distance)
                    _stageStatistics.MaxHomeRunDistance = distance;
            }
        }
    }

}
