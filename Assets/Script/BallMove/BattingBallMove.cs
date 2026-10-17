using Cysharp.Threading.Tasks;
using UnityEngine;

public class BattingBallMove : BallMoveTrajectory
{
    private const float MinVisualSpeed = 0.1f;
    [Header("イベント")]
    [SerializeField] private OnBattingResultEvent _resultEvent;
    [SerializeField] private OnBallLandedEvent _onBallLanded;

    [SerializeField] private float _foulDisplayDistance;
    [Tooltip("着地してからの止まるまでの時間(ms)"), SerializeField] private int _delayBallStopTime;

    private bool _resultResolved;
    private BattingBallResult _result;
    private MeshRenderer _renderer;
    private int _stopVersion;

    public bool IsMoving => _isMoving;
    public bool HasResolved => _resultResolved;
    public BattingBallResult Result => _result;

    public Vector3 Position => transform.position;

    private void OnEnable()
    {
        if (_resultEvent != null)
            _resultEvent.RegisterListener(OnResultReceived);
    }

    private void OnDisable()
    {
        _stopVersion++;
        if (_resultEvent != null)
            _resultEvent.UnregisterListener(OnResultReceived);
    }

    protected override void Update()
    {
        if (!_isMoving) return;
        base.Update();

        if (_resultResolved) return;

        if (_index >= _result.ResolutionIndex)
            ResolveResult();
    }

    private void OnResultReceived(BattingBallResult result)
    {
        if (result == null) return;
        _stopVersion++;
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

        if (result.LandingIndex < 0 || result.LandingIndex >= _trajectory.Count)
        {
            Debug.LogError($"[BattingBall] 到達点のインデックスが軌道の範囲外です: {result.LandingIndex}", this);
            return;
        }

        if (result.ResolutionIndex < 0 || result.ResolutionIndex >= _trajectory.Count)
        {
            Debug.LogError($"[BattingBall] 判定位置のインデックスが軌道の範囲外です: {result.ResolutionIndex}", this);
            return;
        }

        _elapsedTime = 0f;
        _isMoving = false;
        if (!_renderer) _renderer = GetComponentInChildren<MeshRenderer>(true);
        if (_renderer != null) _renderer.enabled = true;

        transform.position = _trajectory[0];
        Debug.Log($"打球結果受信: {result.BallType}, 軌道点数: {_trajectory.Count}");
        StartMoving();
    }

    private async UniTask WaitStopBallAsync(int stopVersion)
    {
        try
        {
            await UniTask.Delay(_delayBallStopTime, cancellationToken: this.GetCancellationTokenOnDestroy());
            if (stopVersion != _stopVersion) return;
            _isMoving = false;
            if (!_renderer) _renderer = GetComponentInChildren<MeshRenderer>(true);
            if (_renderer != null) _renderer.enabled = false;
            else Debug.LogError("打球ボールの描画用MeshRendererが見つかりません。", this);
        }
        catch (System.OperationCanceledException)
        {
        }
    }

    private void StartMoving()
    {
        _elapsedTime = 0f;
        _isMoving = true;
        _resultResolved = false;
    }

    public void SetVisualSpeed(float multiplier)
    {
        _visualSpeedMultiplier = Mathf.Max(MinVisualSpeed, multiplier);
    }

    protected override void ApplySpin()
    {
        _result.SpinAxis.Normalize();
        float degreesPerSecond = _result.SpinRate * BallPhysicsConstants.RPM_TO_DEGREES_PER_SEC * _spinSpeedMultiplier;
        float degreesThisFrame = degreesPerSecond * Time.deltaTime;

        transform.Rotate(_result.SpinAxis, degreesThisFrame, Space.World);
    }

    protected override void OnReachedEnd()
    {
        _isMoving = false;
        if (!_resultResolved)
            ResolveResult();
    }

    private void ResolveResult()
    {
        if (_resultResolved) return;

        _resultResolved = true;
        _onBallLanded?.RaiseEvent();
        WaitStopBallAsync(_stopVersion).Forget();
    }

    public void StopAndHide()
    {
        _stopVersion++;
        _isMoving = false;
        if (!_renderer) _renderer = GetComponentInChildren<MeshRenderer>(true);
        if (_renderer != null) _renderer.enabled = false;
    }
}
