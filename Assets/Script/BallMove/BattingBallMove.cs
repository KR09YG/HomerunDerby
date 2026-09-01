using Cysharp.Threading.Tasks;
using UnityEngine;

public class BattingBallMove : BallMoveTrajectory
{
    [Header("イベント")]
    [SerializeField] private OnBattingResultEvent _resultEvent;
    [SerializeField] private OnBallLandedEvent _onBallLanded;

    [SerializeField] private ScoreCalculator _scoreCalculator;
    [SerializeField] private float _foulDisplayDistance;
    [Tooltip("着地してからの止まるまでの時間(ms)"), SerializeField] private int _delayBallStopTime;

    private bool _hasLanded = false;
    private BattingBallResult _result;

    public bool IsMoving => _isMoving;
    public bool HasLanded => _hasLanded;
    public BattingBallResult Result => _result;

    private void OnEnable()
    {
        if (_resultEvent != null)
            _resultEvent.RegisterListener(OnResultReceived);
    }

    private void OnDisable()
    {
        if (_resultEvent != null)
            _resultEvent.UnregisterListener(OnResultReceived);
    }

    protected override void Update()
    {
        if (!_isMoving) return;
        base.Update();

        if (_hasLanded) return;
        _scoreCalculator.CalculateDistance(transform.position);

        if (_result.LandingIndex < _index)
        {
            Debug.Log($"ボールが着地しました: {_index} > {_result.LandingIndex}");
            if (!_hasLanded)
            {
                Debug.Log("ボールが着地しました");
                _hasLanded = true;
                _onBallLanded?.RaiseEvent();
                WaitStopBallAsync().Forget();
            }
        }
    }

    /// <summary>
    /// 打撃結果受信
    /// </summary>
    private void OnResultReceived(BattingBallResult result)
    {
        _result = result;
        _trajectory = result.TrajectoryPoints;
        Debug.Log($"打球結果受信: {result.BallType}");

        if (result.BallType == BattingBallType.Miss)
            return;

        if (_trajectory == null || _trajectory.Count < 2)
        {
            Debug.LogError("[BattingBall] 軌道未設定");
            return;
        }

        float distance = Vector3.Distance(_trajectory[0], _trajectory[result.LandingIndex]);

        _elapsedTime = 0f;
        _isMoving = false;

        transform.position = _trajectory[0];
        Debug.Log($"打球結果受信: {result.BallType}, 軌道点数: {_trajectory.Count}");
        StartMoving();
    }

    private async UniTaskVoid WaitStopBallAsync()
    {
        await UniTask.Delay(_delayBallStopTime);
        _isMoving = false;
    }

    private void StartMoving()
    {
        _elapsedTime = 0f;
        _isMoving = true;
        _hasLanded = false;
        _scoreCalculator.Initialized(transform.position);
    }

    /// <summary>
    /// 見た目速度変更
    /// </summary>
    public void SetVisualSpeed(float multiplier)
    {
        _visualSpeedMultiplier = Mathf.Max(0.1f, multiplier);
    }

    protected override void ApplySpin()
    {
        _result.SpinAxis.Normalize();
        float deg = _result.SpinRate * 360f / 60f * _spinSpeedMultiplier;
    }

    /// <summary>
    /// 軌道終了到達
    /// </summary>
    protected override void OnReachedEnd()
    {

    }
}
