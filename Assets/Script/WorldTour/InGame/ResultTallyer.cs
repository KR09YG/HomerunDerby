using UnityEngine;

public class ResultTallyer : MonoBehaviour
{
    private const float CenterBoundaryDegrees = 15f;
    private const float FairBoundaryDegrees = 45f;

    [SerializeField] private OnBattingResultEvent _onBattingResultEvent;
    [SerializeField] private OnAtBatResetEvent _onAtBatResetEvent;
    [SerializeField] private BattingDistanceTracker _distanceTracker;
    private StageStatistics _stageStatistics;
    public StageStatistics StageStatistics => _stageStatistics;
    private readonly Vector3 _homePlatePosition = Vector3.zero;
    private bool _isReceivedResult;

    private void Awake()
    {
        if (_onBattingResultEvent) _onBattingResultEvent.RegisterListener(OnBattingResultReceived);
        else Debug.LogError("OnBattingResultEventが設定されていません");
        if (_onAtBatResetEvent) _onAtBatResetEvent.RegisterListener(OnAtBatReset);
        else Debug.LogError("OnAtBatResetEventが設定されていません");
    }

    private void OnDestroy()
    {
        if (_onBattingResultEvent) _onBattingResultEvent.UnregisterListener(OnBattingResultReceived);
        if (_onAtBatResetEvent) _onAtBatResetEvent.UnregisterListener(OnAtBatReset);
    }

    /// <summary>
    /// ステージで共有する集計先を設定し、打席の受信状態を初期化する。
    /// </summary>
    public void Initialize(StageStatistics stageStatistics)
    {
        _stageStatistics = stageStatistics;
        _isReceivedResult = false;
    }

    private void OnAtBatReset()
    {
        _isReceivedResult = false;
    }

    private void OnBattingResultReceived(BattingBallResult result)
    {
        if (_stageStatistics == null || result == null)
        {
            Debug.LogError("ResultTallyerが初期化されていないか、結果がnullです。");
            return;
        }

        if (_isReceivedResult)
        {
            Debug.LogWarning("すでに結果を受け取っています。");
            return;
        }

        _isReceivedResult = true;
        // フェアの打球だけを総飛距離に加える。
        if (result.BallType == BattingBallType.HomeRun ||
            result.BallType == BattingBallType.Hit ||
            result.BallType == BattingBallType.GroundBall)
        {
            float distance = _distanceTracker.CalculateDistance(
                result.TrajectoryPoints[result.LandingIndex], result.TrajectoryPoints[0]);
            _stageStatistics.TotalFairDistance += distance;

            if (result.BallType == BattingBallType.HomeRun)
            {
                _stageStatistics.HomeRunCount++;
                if (_stageStatistics.MaxHomeRunDistance < distance)
                    _stageStatistics.MaxHomeRunDistance = distance;

                HomeRunDirection direction =
                    GetHomeRunDirection(result.TrajectoryPoints[result.LandingIndex]);
                switch (direction)
                {
                    case HomeRunDirection.Left:
                        _stageStatistics.LeftHomeRunCount++;
                        break;
                    case HomeRunDirection.Right:
                        _stageStatistics.RightHomeRunCount++;
                        break;
                    case HomeRunDirection.Center:
                        _stageStatistics.CenterHomeRunCount++;
                        break;
                    default:
                        Debug.LogWarning("ホームランの方向が不明です。");
                        break;
                }
            }
        }
    }

    private HomeRunDirection GetHomeRunDirection(Vector3 landingPosition)
    {
        Vector3 direction = landingPosition - _homePlatePosition;
        direction.y = 0f;
        // センターは-Z。右方向が負角、左方向が正角になる。
        float angle = Vector3.SignedAngle(Vector3.back, direction, Vector3.up);

        if (angle >= -FairBoundaryDegrees && angle < -CenterBoundaryDegrees)
            return HomeRunDirection.Right;
        if (angle > CenterBoundaryDegrees && angle <= FairBoundaryDegrees)
            return HomeRunDirection.Left;
        if (angle >= -CenterBoundaryDegrees && angle <= CenterBoundaryDegrees)
            return HomeRunDirection.Center;
        return HomeRunDirection.None;
    }
}
