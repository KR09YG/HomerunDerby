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
    private SpinState _spin;
    private Quaternion _releaseRotation;
    [SerializeField, Tooltip("Sceneビューで総回転軸と進行方向を表示する。")]
    private bool _showSpinAxes;

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

    public void Setup(List<Vector3> trajectory, float deltaTime, SpinState spin)
    {
        Debug.Log($"{trajectory.Count}点の軌道でボール移動を初期化します");
        _elapsedTime = 0f;
        _trajectoryProgress = 0f;
        IsReach = false;
        _isMoving = false;
        _trajectory = trajectory;
        _trajectoryDeltaTime = deltaTime;
        _spin = spin;
        _releaseRotation = transform.rotation;
        transform.position = trajectory[0];
        if (_renderer == null)
            _renderer = GetComponent<MeshRenderer>();
        _renderer.enabled = true;
        StartMoving();
    }

    public void ResetIsReach() => IsReach = false;

    // 打撃イベントの時刻に合わせ、軌道上の位置と回転を同じ時間まで戻す。
    public void RewindForContact(float gameSeconds)
    {
        if (!_isMoving || _trajectory == null || _trajectory.Count < 2 ||
            _trajectoryDeltaTime <= 0f || gameSeconds <= 0f) return;
        _elapsedTime = Mathf.Max(0f, _elapsedTime - gameSeconds * _visualSpeedMultiplier);
        _index = Mathf.Clamp(Mathf.FloorToInt(_elapsedTime / _trajectoryDeltaTime), 0, _trajectory.Count - 2);
        float t = (_elapsedTime - _index * _trajectoryDeltaTime) / _trajectoryDeltaTime;
        transform.position = Vector3.Lerp(_trajectory[_index], _trajectory[_index + 1], t);
        if (_enableSpin) ApplySpin();
    }

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
        // 軌道と同じ時計を使い、スロー再生や打撃時の停止でも回転位相を揃える。
        transform.rotation = _spin.RotationAfter(_elapsedTime * _spinSpeedMultiplier) * _releaseRotation;
    }

    private void OnDrawGizmos()
    {
        if (!_showSpinAxes || _trajectory == null || _trajectory.Count < 2) return;
        Gizmos.color = Color.cyan;
        Vector3 axis = _spin.AngularVelocityRadPerSec.normalized;
        Gizmos.DrawLine(transform.position - axis * .2f, transform.position + axis * .2f);
        int index = Mathf.Clamp(_index, 0, _trajectory.Count - 2);
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(transform.position, (_trajectory[index + 1] - _trajectory[index]).normalized * .3f);
    }

    protected override void OnReachedEnd()
    {
        _elapsedTime = (_trajectory.Count - 1) * _trajectoryDeltaTime;
        Debug.Log("PitchBallMove: ボールがターゲットに到達しました");
        _onBallReachedTarget?.RaiseEvent(this);
        _isMoving = false;
        _trajectory = null;
        IsReach = true;
    }
}
