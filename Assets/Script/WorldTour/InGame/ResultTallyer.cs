using UnityEngine;

public class ResultTallyer : MonoBehaviour
{
    [SerializeField] OnBattingResultEvent _onBattingResultEvent;
    [SerializeField] OnAtBatResetEvent _onAtBatResetEvent;
    [SerializeField] BattingDistanceTracker _distanceTracker;
    private StageStatistics _stageStatistics;
    public StageStatistics StageStatistics => _stageStatistics;
    private Vector3 _homebasePos = Vector3.zero;
    private bool _isReceivedResult;

    private void Awake()
    {
        if (_onBattingResultEvent) _onBattingResultEvent.RegisterListener(OnBattingResultReceive);
        else Debug.LogError("OnBattingResultEventが設定されていません");
        if (_onAtBatResetEvent) _onAtBatResetEvent.RegisterListener(OnAtBatReset);
        else Debug.LogError("OnAtBatResetEventが設定されていません");
    }

    private void OnDestroy()
    {
        if (_onBattingResultEvent) _onBattingResultEvent.UnregisterListener(OnBattingResultReceive);
        if (_onAtBatResetEvent) _onAtBatResetEvent.UnregisterListener(OnAtBatReset);
    }

    /// <summary>
    /// イベントかマネージャークラスに呼び出してもらう
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

    private void OnBattingResultReceive(BattingBallResult result)
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
                // ホームランの数をカウントする
                _stageStatistics.HomeRunCount++;
                // 最大のホームラン距離を更新する
                if (_stageStatistics.MaxHomeRunDistance < distance)
                    _stageStatistics.MaxHomeRunDistance = distance;

                // ホームランの方向を判定する
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
        // _stageStatisticsの内容をデバッグログに出力する
        Debug.Log($"[ResultTallyer]TotalFairDistance: {_stageStatistics.TotalFairDistance}, " +
                  $"HomeRunCount: {_stageStatistics.HomeRunCount}, " +
                  $"MaxHomeRunDistance: {_stageStatistics.MaxHomeRunDistance}, " +
                  $"LeftHomeRunCount: {_stageStatistics.LeftHomeRunCount}, " +
                  $"RightHomeRunCount: {_stageStatistics.RightHomeRunCount}, " +
                  $"CenterHomeRunCount: {_stageStatistics.CenterHomeRunCount}");
    }

    private HomeRunDirection GetHomeRunDirection(Vector3 LandingPos)
    {
        Vector3 direction = LandingPos - _homebasePos;
        // 水平の角度を計算するためにy成分を0にする
        direction.y = 0f;
        // 真正面からの角度を計算,正面が-Z方向、左が+X方向、右が-X方向
        float angle = Vector3.SignedAngle(Vector3.back, direction, Vector3.up);

        switch (angle)
        {
            case float a when a < -15f && a >= -45f:
                Debug.Log($"ホームランの方向: Left, 角度: {angle}");
                return HomeRunDirection.Right;
            case float a when a > 15f && a <= 45f:
                Debug.Log($"ホームランの方向: Right, 角度: {angle}");
                return HomeRunDirection.Left;
            case float a when a <= 15f && a >= -15f:
                Debug.Log($"ホームランの方向: Center, 角度: {angle}");
                return HomeRunDirection.Center;
            default:
                Debug.LogWarning($"ホームランの方向が不明です。角度: {angle}");
                return HomeRunDirection.None;
        }
    }
}
