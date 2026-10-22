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

    [Header("打球の軌跡")]
    [SerializeField] private Material _trailMaterial;
    [SerializeField, Min(0.05f)] private float _trailDuration = 1.25f;
    [SerializeField, Min(0.005f)] private float _trailWidth = 0.12f;

    private bool _resultResolved;
    private BattingBallResult _result;
    private MeshRenderer _renderer;
    private TrailRenderer _trail;
    private int _stopVersion;

    public bool IsMoving => _isMoving;
    public bool HasResolved => _resultResolved;
    public BattingBallResult Result => _result;

    public Vector3 Position => transform.position;

    private void Awake()
    {
        if (_trailMaterial == null) return;

        _trail = GetComponent<TrailRenderer>();
        if (_trail == null) _trail = gameObject.AddComponent<TrailRenderer>();
        _trail.sharedMaterial = _trailMaterial;
        _trail.time = _trailDuration;
        _trail.startWidth = _trailWidth;
        _trail.endWidth = 0f;
        _trail.minVertexDistance = 0.03f;
        _trail.alignment = LineAlignment.View;
        _trail.numCornerVertices = 3;
        _trail.numCapVertices = 3;
        _trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _trail.receiveShadows = false;
        _trail.colorGradient = new Gradient
        {
            colorKeys = new[]
            {
                new GradientColorKey(new Color(0.85f, 0.18f, 0.04f), 0f),
                new GradientColorKey(new Color(1f, 0.48f, 0.04f), 0.55f),
                new GradientColorKey(new Color(1f, 0.92f, 0.18f), 1f)
            },
            alphaKeys = new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.95f, 0.3f),
                new GradientAlphaKey(1f, 1f)
            }
        };
        _trail.emitting = false;
    }

    private void OnEnable()
    {
        if (_resultEvent != null)
            _resultEvent.RegisterListener(OnResultReceived);
    }

    private void OnDisable()
    {
        _stopVersion++;
        ClearTrail();
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
        ClearTrail();
        _result = result;
        _trajectory = result.TrajectoryPoints;

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
        if (_trail != null) _trail.emitting = true;
        StartMoving();
    }

    private async UniTask WaitStopBallAsync(int stopVersion)
    {
        try
        {
            await UniTask.Delay(_delayBallStopTime, cancellationToken: this.GetCancellationTokenOnDestroy());
            if (stopVersion != _stopVersion) return;
            _isMoving = false;
            ClearTrail();
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
        ClearTrail();
        if (!_renderer) _renderer = GetComponentInChildren<MeshRenderer>(true);
        if (_renderer != null) _renderer.enabled = false;
    }

    private void ClearTrail()
    {
        if (_trail == null) return;
        _trail.emitting = false;
        _trail.Clear();
    }
}
