using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class PitchBallMove : BallMoveTrajectory
{
    [SerializeField] private OnBattingResultEvent _onBattingResult;
    [SerializeField] private OnBallReachedTargetEvent _onBallReachedTarget;
    public List<Vector3> Trajectory => _trajectory;
    public bool IsMoving => _isMoving;
    public bool IsReach { get; private set; }

    private MeshRenderer _renderer;
    private Vector3 _spinAxis;
    private float _spinRate;

    private void Awake()
    {
        if (_onBattingResult != null) _onBattingResult.RegisterListener(BattingResultReceive);
        else Debug.LogWarning("OnBattingResultEvent が未設定です");
    }

    private void BattingResultReceive(BattingBallResult result)
    {
        // ボールが打たれた場合の処理
        if (result.BallType != BattingBallType.Miss)
        {
            _isMoving = false;
        }
    }

    public void Setup(
        List<Vector3> trajectory,
        float deltaTime,
        Vector3 spinAxis,
        float spinRate)
    {
        Debug.Log($"{trajectory.Count}点の軌道でボール移動を初期化します");
        _elapsedTime = 0f;
        _trajectoryProgress = 0f;
        IsReach = false;
        _isMoving = false;
        _trajectory = trajectory;
        _trajectoryDeltaTime = deltaTime;
        _spinAxis = spinAxis;
        _spinRate = spinRate;
        transform.position = trajectory[0];
        if (_renderer == null)
            _renderer = GetComponent<MeshRenderer>();
        _renderer.enabled = true;
        StartMoving();
    }

    public void ResetIsReach() => IsReach = false;

    public void StartMoving()
    {
        _isMoving = true;
    }

    protected override void Update()
    {
        if (!_isMoving) return;
        base.Update();
    }

    protected override void ApplySpin()
    {
        float deg = _spinRate * 360f / 60f * _spinSpeedMultiplier;
        transform.Rotate(_spinAxis, deg * Time.deltaTime);
    }

    protected override void OnReachedEnd()
    {
        Debug.Log("PitchBallMove: ボールがターゲットに到達しました");
        _onBallReachedTarget?.RaiseEvent(this);
        _isMoving = false;
        _trajectory = null;
        _elapsedTime = 0f;
        IsReach = true;
    }
}