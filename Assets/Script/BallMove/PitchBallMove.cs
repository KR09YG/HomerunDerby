using System.Collections.Generic;
using UnityEngine;

public class PitchBallMove : BallMoveTrajectory
{
    [SerializeField] private OnBattingResultEvent _onBattingResult;
    [SerializeField] private OnBallReachedTargetEvent _onBallReachedTarget;
    public List<Vector3> Trajectory => _trajectory;
    public bool IsMoving => _isMoving;
    public bool HasReachedTarget { get; private set; }

    private MeshRenderer _renderer;
    private BallDistanceVisualScaler _visualScaler;
    private SpinState _spin;
    private Quaternion _releaseRotation;
    private bool _hitStopPaused;
    [SerializeField, Tooltip("Sceneビューで総回転軸と進行方向を表示する。")]
    private bool _showSpinAxes;

    private void Awake()
    {
        _visualScaler = GetComponent<BallDistanceVisualScaler>();
        if (_onBattingResult != null) _onBattingResult.RegisterListener(BattingResultReceive);
        else Debug.LogWarning("OnBattingResultEvent が未設定です");
    }

    private void OnDestroy()
    {
        _onBattingResult?.UnregisterListener(BattingResultReceive);
    }

    private void BattingResultReceive(BattingBallResult result)
    {
        if (result.BallType != BattingBallType.Miss)
        {
            _isMoving = false;
            _visualScaler?.SetScalingActive(false);
        }
    }

    public void Setup(List<Vector3> trajectory, float deltaTime, SpinState spin)
    {
        _elapsedTime = 0f;
        _trajectoryProgress = 0f;
        HasReachedTarget = false;
        _isMoving = false;
        _hitStopPaused = false;
        _trajectory = trajectory;
        _trajectoryDeltaTime = deltaTime;
        _spin = spin;
        _releaseRotation = transform.rotation;
        transform.position = trajectory[0];
        if (_renderer == null)
            _renderer = GetComponentInChildren<MeshRenderer>(true);
        if (_renderer != null) _renderer.enabled = true;
        else Debug.LogError("投球ボールの描画用MeshRendererが見つかりません。", this);
        _visualScaler?.SetScalingActive(true);
        StartMoving();
    }

    public void ResetReachedTarget() => HasReachedTarget = false;

    public void SetHitStopPaused(bool paused) => _hitStopPaused = paused;

    // 表示倍率を掛けず、現在の軌道区間を物理計算の秒数で割る。
    public bool TryGetPhysicalVelocity(out Vector3 velocityMps)
    {
        velocityMps = Vector3.zero;
        if (_trajectory == null || _trajectory.Count < 2 || _trajectoryDeltaTime <= 0f ||
            float.IsNaN(_trajectoryDeltaTime) || float.IsInfinity(_trajectoryDeltaTime) ||
            _elapsedTime < 0f || float.IsNaN(_elapsedTime) || float.IsInfinity(_elapsedTime) ||
            _elapsedTime > (_trajectory.Count - 1) * _trajectoryDeltaTime) return false;

        int index = Mathf.Clamp(Mathf.FloorToInt(_elapsedTime / _trajectoryDeltaTime), 0, _trajectory.Count - 2);
        velocityMps = BaseballWorldScale.ToMeters(_trajectory[index + 1] - _trajectory[index]) / _trajectoryDeltaTime;
        return !float.IsNaN(velocityMps.sqrMagnitude) && !float.IsInfinity(velocityMps.sqrMagnitude);
    }

    // 表示上の再生速度を含めて先の座標を読む。ボール本体の時刻や姿勢は変更しない。
    public bool TryGetPredictedPosition(float gameSeconds, out Vector3 position)
    {
        position = transform.position;
        if (!_isMoving || _trajectory == null || _trajectory.Count < 2 ||
            _trajectoryDeltaTime <= 0f || gameSeconds < 0f ||
            float.IsNaN(gameSeconds) || float.IsInfinity(gameSeconds)) return false;

        float sampleTime = _elapsedTime + gameSeconds * _visualSpeedMultiplier;
        float endTime = (_trajectory.Count - 1) * _trajectoryDeltaTime;
        if (sampleTime > endTime)
        {
            position = _trajectory[_trajectory.Count - 1];
            return false;
        }
        int index = Mathf.Clamp(Mathf.FloorToInt(sampleTime / _trajectoryDeltaTime), 0, _trajectory.Count - 2);
        float t = (sampleTime - index * _trajectoryDeltaTime) / _trajectoryDeltaTime;
        position = Vector3.Lerp(_trajectory[index], _trajectory[index + 1], t);
        return true;
    }

    // 現在の再生時刻から、指定したZ平面を横切るまでの時間を返す。
    // 通過後は負の値になるため、打撃入力の遅れも表示できる。
    public bool TryGetTimeUntilZ(float targetZ, out float gameSeconds)
    {
        gameSeconds = 0f;
        if (!_isMoving || _trajectory == null || _trajectory.Count < 2 ||
            _trajectoryDeltaTime <= 0f || _visualSpeedMultiplier <= 0f ||
            float.IsNaN(_elapsedTime) || float.IsInfinity(_elapsedTime) ||
            float.IsNaN(targetZ) || float.IsInfinity(targetZ)) return false;

        for (int i = 0; i < _trajectory.Count - 1; i++)
        {
            float fromZ = _trajectory[i].z;
            float toZ = _trajectory[i + 1].z;
            if (float.IsNaN(fromZ) || float.IsInfinity(fromZ) ||
                float.IsNaN(toZ) || float.IsInfinity(toZ)) continue;
            if ((fromZ - targetZ) * (toZ - targetZ) > 0f) continue;

            float span = toZ - fromZ;
            if (Mathf.Abs(span) < 0.000001f) continue;
            float fraction = (targetZ - fromZ) / span;
            if (fraction < 0f || fraction > 1f) continue;
            float crossingTime = (i + fraction) * _trajectoryDeltaTime;
            gameSeconds = (crossingTime - _elapsedTime) / _visualSpeedMultiplier;
            return true;
        }

        return false;
    }

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
        if (!_isMoving || _hitStopPaused) return;
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
        _isMoving = false;
        HasReachedTarget = true;
        _onBallReachedTarget?.RaiseEvent(this);
        _trajectory = null;
    }
}
