using UnityEngine;

[DefaultExecutionOrder(1100)]
public class BattingSystem : MonoBehaviour
{
    private const float MillisecondsPerSecond = 1000f;
    private enum ContactTarget { Center, Left, Right, Up, Down }

    [SerializeField] private OnBattingInputEvent _inputEvent;
    [SerializeField] private OnSwingEvent _swingEvent;
    [SerializeField] private OnBallReachedTargetEvent _ballReachedTargetEvent;
    [SerializeField] private OnPitchBallReleaseEvent _releaseEvent;
    [SerializeField] private OnBattingHitEvent _hitEvent;
    [SerializeField] private BattingParameters _parameters;
    [SerializeField] private PitchBallMove _ballMove;
    [Tooltip("ヒット時に停止する実時間。空振りは停止せず、0でヒット時も停止なし。")]
    [SerializeField, Min(0f)] private float _hitPauseSeconds = 0.1f;
    [Header("打撃時のボール表示")]
    [Tooltip("ミートポイントを中心とした左右上下の位置間隔（m）。")]
    [SerializeField, Min(0f)] private float _contactPointSpacingMeters = 0.05f;
    [Tooltip("ボールの中心をバットから投手側へ離す距離（m）。")]
    [SerializeField, Min(0f)] private float _contactSurfaceOffsetMeters = 0.06f;
    [Tooltip("この範囲内のずれは中心に移動する（m）。")]
    [SerializeField, Min(0f)] private float _centerContactThresholdMeters = 0.08f;
    private bool _impactSent;
    private Coroutine _hitStopRoutine;
    private Animator _batterAnimator;
    private float _animatorSpeedBeforePause;
    private bool _hitStopActive;
    private bool _canSwing = true;
    private bool _isSwinging = false;
    private BatSwingAimIK _swingIK;
    private bool _contactPending;
    private bool _contactCaptured;
    private Vector3 _contactBallPosition;
    private Vector3 _contactCursorPosition;
    private Vector3 _contactBatSweetSpot;
    private Vector3 _contactLaunchPosition;
    private readonly Vector3[] _contactTargets = new Vector3[5];
    private ContactTarget _selectedContactTarget;
    private bool _hasContactCursor;
    private bool _hasContactBatSweetSpot;
    private bool _hasContactLaunchPosition;
    private Vector3 _contactPitchVelocityMps;
    private bool _contactVelocityCaptured;
    private float _swingStartedTime;
    private float _contactRewindSeconds;

    public BattingParameters Parameters => _parameters;
    public PitchBallMove CurrentPitch => _ballMove;
    public bool CanSwing => _canSwing && !_isSwinging;
    public bool IsHitStopped => _hitStopActive;
    public event System.Action<PitchBallMove> PitchReleased;
    public event System.Action SwingStarted;
    public event System.Action ContactCaptured;

    private void Awake()
    {
        _swingIK = GetComponent<BatSwingAimIK>();
        _batterAnimator = GetComponentInChildren<Animator>();
        if (_releaseEvent == null) Debug.LogError("[BattingSystem] _releaseEvent is not assigned!");
        else _releaseEvent.RegisterListener(ReleasedBall);
        if (_ballReachedTargetEvent == null) Debug.LogError("[BattingSystem] _ballReachedTargetEvent is not assigned!");
        else _ballReachedTargetEvent.RegisterListener(OnBallReachedTarget);

        _canSwing = false;
        _isSwinging = false;
    }

    private void OnDestroy()
    {
        _releaseEvent?.UnregisterListener(ReleasedBall);
        _ballReachedTargetEvent?.UnregisterListener(OnBallReachedTarget);
        EndHitStop();

    }

    private void OnDisable() => EndHitStop();

    private void Update()
    {
        if (Input.GetMouseButtonDown(0)) TrySwing();
    }

    private bool TrySwing()
    {
        if (!CanSwing) return false;

        _isSwinging = true;
        _canSwing = false;
        _swingStartedTime = Time.time;
        _hasContactCursor = _swingIK != null && _swingIK.Cursor != null;
        if (_hasContactCursor) _contactCursorPosition = _swingIK.Cursor.CurrentPos;
        if (_parameters == null || _parameters.EnableDebugLogs)
            Debug.Log($"[Batting/Input] time={Time.time:F4} ball={(_ballMove != null ? _ballMove.transform.position.ToString("F4") : "null")}", this);
        _inputEvent.RaiseEvent();
        SwingStarted?.Invoke();
        return true;
    }

    private void OnBallReachedTarget(PitchBallMove ball)
    {
        _canSwing = false;
        _isSwinging = true;
    }

    public void StartBattingCalculate()
    {
        if (_impactSent) return;
        if (!_isSwinging)
        {
            Debug.LogWarning("[Batting/Event] Ignored: swing input has not been accepted.", this);
            return;
        }
        _impactSent = true;
        _contactCaptured = false;
        _hasContactLaunchPosition = false;
        _contactRewindSeconds = _swingIK != null ? _swingIK.ContactRewindSeconds : 0f;
        bool poseRequested = _swingIK != null && _swingIK.RequestContactPose();
        if (!poseRequested) _contactRewindSeconds = 0f;
        if (_ballMove != null) _ballMove.RewindForContact(_contactRewindSeconds);
        _contactBallPosition = _ballMove != null ? _ballMove.transform.position : Vector3.zero;
        // 停止や次の投球に影響されないよう、巻き戻した座標と同じ時刻の速度を残す。
        _contactVelocityCaptured = _ballMove != null && _ballMove.TryGetPhysicalVelocity(out _contactPitchVelocityMps);
        // 次のフレームへ進む前に投球だけ止め、IK適用後にヒットかどうかを確定する。
        _ballMove?.SetHitStopPaused(true);
        _contactPending = true;
    }

    private void LateUpdate()
    {
        if (!_contactPending) return;
        _contactPending = false;
        _contactCaptured = _ballMove != null && _hasContactCursor;
        _hasContactBatSweetSpot = _swingIK != null && _swingIK.TryGetSweetSpot(out _contactBatSweetSpot);
        if (HasHitContact() && _hasContactBatSweetSpot && _contactVelocityCaptured)
        {
            _contactLaunchPosition = SelectContactTarget();
            _hasContactLaunchPosition = true;
            _ballMove.transform.position = _contactLaunchPosition;
        }
        if (_hitPauseSeconds > 0f && HasHitContact()) BeginHitStop();
        else _ballMove?.SetHitStopPaused(false);
        LogContact();
        ContactCaptured?.Invoke();
        // 空振りは記録だけ残して振り抜き、ヒットした場合だけ停止する。
        if (_hitStopActive)
            _hitStopRoutine = StartCoroutine(HoldContactBeforeCalculation());
        else
            CalculateNow();
    }

    private void BeginHitStop()
    {
        _hitStopActive = true;
        if (_batterAnimator == null) return;
        _animatorSpeedBeforePause = _batterAnimator.speed;
        _batterAnimator.speed = 0f;
    }

    private bool HasHitContact()
    {
        return _contactCaptured && _parameters != null &&
            BaseballWorldScale.ToMeters(Vector3.Distance(_contactCursorPosition, _contactBallPosition)) <=
            _parameters.MaxImpactDistance;
    }

    private Vector3 SelectContactTarget()
    {
        Vector3 pitchDirection = _contactPitchVelocityMps.sqrMagnitude > 0.0001f
            ? _contactPitchVelocityMps.normalized : Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, pitchDirection).normalized;
        if (right.sqrMagnitude < 0.001f) right = Vector3.right;
        Vector3 up = Vector3.Cross(pitchDirection, right).normalized;
        Vector3 center = _contactBatSweetSpot -
            pitchDirection * BaseballWorldScale.ToWorldUnits(_contactSurfaceOffsetMeters);
        float spacing = BaseballWorldScale.ToWorldUnits(_contactPointSpacingMeters);
        _contactTargets[(int)ContactTarget.Center] = center;
        _contactTargets[(int)ContactTarget.Left] = center - right * spacing;
        _contactTargets[(int)ContactTarget.Right] = center + right * spacing;
        _contactTargets[(int)ContactTarget.Up] = center + up * spacing;
        _contactTargets[(int)ContactTarget.Down] = center - up * spacing;

        Vector3 offset = BaseballWorldScale.ToMeters(_contactBallPosition - _contactCursorPosition);
        if (new Vector2(offset.x, offset.y).magnitude <= _centerContactThresholdMeters)
            _selectedContactTarget = ContactTarget.Center;
        else if (Mathf.Abs(offset.x) >= Mathf.Abs(offset.y))
            _selectedContactTarget = offset.x < 0f ? ContactTarget.Left : ContactTarget.Right;
        else
            _selectedContactTarget = offset.y > 0f ? ContactTarget.Up : ContactTarget.Down;
        return _contactTargets[(int)_selectedContactTarget];
    }

    private void LogContact()
    {
        if (_parameters != null && !_parameters.EnableDebugLogs) return;
        Vector3 delta = BaseballWorldScale.ToMeters(_contactBallPosition - _contactCursorPosition);
        float limit = _parameters != null ? _parameters.MaxImpactDistance : 0f;
        float distance = delta.magnitude;
        string reason = _ballMove == null ? "NO_BALL" : !_contactCaptured ? "NO_CURSOR" :
            _parameters == null ? "NO_PARAMETERS" : distance <= limit ? "HIT" :
            Mathf.Abs(delta.z) > limit ? "MISS_DEPTH_TIMING" :
            new Vector2(delta.x, delta.y).magnitude > limit ? "MISS_XY_AIM" : "MISS_COMBINED_DISTANCE";
        Debug.Log($"[Batting/Contact] result={reason} clipFrame={(_swingIK != null ? _swingIK.ContactClipFrame : -1f):F2} " +
            $"inputToEvent={Time.time - _swingStartedTime:F4}s rewind={_contactRewindSeconds * MillisecondsPerSecond:F2}ms " +
            $"cursor={(_hasContactCursor ? _contactCursorPosition.ToString("F4") : "unavailable")} " +
            $"bat={(_hasContactBatSweetSpot ? _contactBatSweetSpot.ToString("F4") : "unavailable")} " +
            $"ball={(_ballMove != null ? _contactBallPosition.ToString("F4") : "null")} " +
            $"contactTarget={(_hasContactLaunchPosition ? _selectedContactTarget.ToString() : "unavailable")} " +
            $"launch={(_hasContactLaunchPosition ? _contactLaunchPosition.ToString("F4") : "unavailable")} " +
            $"deltaXYZ={(_contactCaptured ? delta.ToString("F4") : "unavailable")} distance={(_contactCaptured ? distance.ToString("F4") : "unavailable")} limit={limit:F4} " +
            $"ballMoving={(_ballMove != null && _ballMove.IsMoving)} pause={(HasHitContact() ? _hitPauseSeconds : 0f):F2}s", this);
    }

    private void OnDrawGizmos()
    {
        if (!_hitStopActive || !_contactCaptured || _parameters == null || !_parameters.ShowTrajectoryGizmos) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(_contactCursorPosition, BaseballWorldScale.ToWorldUnits(_parameters.MaxImpactDistance));
        Gizmos.DrawSphere(_contactCursorPosition, .02f);
        if (_hasContactBatSweetSpot)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(_contactBatSweetSpot, .02f);
        }
        if (_hasContactLaunchPosition)
        {
            Gizmos.color = Color.green;
            foreach (Vector3 target in _contactTargets) Gizmos.DrawWireSphere(target, .025f);
            Gizmos.DrawSphere(_contactLaunchPosition, .035f);
        }
        Gizmos.color = Color.red;
        Gizmos.DrawLine(_contactCursorPosition, _contactBallPosition);
        Gizmos.DrawWireSphere(_contactBallPosition, .035f);
    }

    public bool TryGetContact(PitchBallMove ball, out Vector3 cursorPosition, out Vector3 ballPosition)
    {
        cursorPosition = _contactCursorPosition;
        ballPosition = _contactBallPosition;
        return _contactCaptured && ball != null && ball == _ballMove;
    }

    public bool TryGetContactVelocity(PitchBallMove ball, out Vector3 velocityMps)
    {
        velocityMps = _contactPitchVelocityMps;
        return _contactCaptured && _contactVelocityCaptured && ball != null && ball == _ballMove;
    }

    public bool TryGetContactLaunchPosition(PitchBallMove ball, out Vector3 position)
    {
        position = _contactLaunchPosition;
        return _contactCaptured && _hasContactLaunchPosition && ball != null && ball == _ballMove;
    }

    private void CalculateNow()
    {
        Debug.Log("[BattingSystem] Batting calculation started.");
        _hitEvent?.RaiseEvent(_ballMove);
        _swingEvent?.RaiseEvent();
    }

    private System.Collections.IEnumerator HoldContactBeforeCalculation()
    {
        yield return new WaitForSecondsRealtime(_hitPauseSeconds);
        RestoreHitStop();
        _hitStopRoutine = null;
        CalculateNow();
    }

    private void EndHitStop()
    {
        _contactPending = false;
        _contactCaptured = false;
        _contactVelocityCaptured = false;
        _hasContactLaunchPosition = false;
        _hasContactCursor = false;
        _hasContactBatSweetSpot = false;
        if (_hitStopRoutine != null) StopCoroutine(_hitStopRoutine);
        RestoreHitStop();
        _hitStopRoutine = null;
    }

    private void RestoreHitStop()
    {
        _ballMove?.SetHitStopPaused(false);
        if (_hitStopActive && _batterAnimator != null)
            _batterAnimator.speed = _animatorSpeedBeforePause;
        _hitStopActive = false;
    }

    private void ReleasedBall(PitchBallMove ball)
    {
        Debug.Log("[BattingSystem] Ball Released - Swing is now allowed.");
        EndHitStop();
        _canSwing = true;
        _isSwinging = false;
        _impactSent = false;
        _ballMove = ball;
        PitchReleased?.Invoke(ball);
    }
}
