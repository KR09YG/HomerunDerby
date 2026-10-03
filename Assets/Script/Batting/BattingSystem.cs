using UnityEngine;

[DefaultExecutionOrder(1100)]
public class BattingSystem : MonoBehaviour
{
    [SerializeField] private OnBattingInputEvent _inputEvent;
    [SerializeField] private OnSwingEvent _swingEvent;
    [SerializeField] private OnBallReachedTargetEvent _ballReachedTargetEvent;
    [SerializeField] private OnPitchBallReleaseEvent _releseEvent;
    [SerializeField] private OnBattingHitEvent _hitEvent;
    [SerializeField] private BattingParameters _parameters;
    [SerializeField] private PitchBallMove _ballmove;
    [Tooltip("打撃イベントでヒット・空振りの両方を停止する実時間。0で停止なし。")]
    [SerializeField, Min(0f)] private float _hitPauseSeconds = 3f;
    private bool _impactSent;
    private Coroutine _pauseRoutine;
    private float _timeScaleBeforePause = 1f;
    private bool _pauseApplied;
    private bool _canSwing = true;
    private bool _isSwinging = false;
    private BatSwingAimIK _swingIK;
    private bool _contactPending;
    private bool _contactCaptured;
    private Vector3 _contactBallPosition;
    private Vector3 _contactSweetSpot;
    private float _swingStartedTime;
    private float _contactRewindSeconds;

    private void Awake()
    {
        _swingIK = GetComponent<BatSwingAimIK>();
        if (_releseEvent == null) Debug.LogError("[BattingSystem] _releseEvent is not assigned!");
        else _releseEvent.RegisterListener(ReleasedBall);
        if (_ballReachedTargetEvent == null) Debug.LogError("[BattingSystem] _ballReachedTargetEvent is not assigned!");
        else _ballReachedTargetEvent.RegisterListener(OnRiacheTarget);

        _canSwing = false;
        _isSwinging = false;
    }

    private void OnDestroy()
    {
        _releseEvent?.UnregisterListener(ReleasedBall);
        _ballReachedTargetEvent?.UnregisterListener(OnRiacheTarget);
        EndHitPause();

    }

    private void OnDisable() => EndHitPause();

    private void Update()
    {
        if (_canSwing && Input.GetMouseButtonDown(0) && !_isSwinging)
        {
            _isSwinging = true;
            _canSwing = false;
            _swingStartedTime = Time.time;
            if (_parameters == null || _parameters.EnableDebugLogs)
                Debug.Log($"[Batting/Input] time={Time.time:F4} ball={(_ballmove != null ? _ballmove.transform.position.ToString("F4") : "null")}", this);
            _inputEvent.RaiseEvent();
        }

    }

    private void OnRiacheTarget(PitchBallMove ball)
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
        _contactRewindSeconds = _swingIK != null ? _swingIK.ContactRewindSeconds : 0f;
        bool poseRequested = _swingIK != null && _swingIK.RequestContactPose();
        if (!poseRequested) _contactRewindSeconds = 0f;
        if (_ballmove != null) _ballmove.RewindForContact(_contactRewindSeconds);
        _contactBallPosition = _ballmove != null ? _ballmove.transform.position : Vector3.zero;
        // Freeze inside the event, but capture the bat only after this frame's IK.
        _timeScaleBeforePause = Time.timeScale;
        Time.timeScale = 0f;
        _pauseApplied = true;
        _contactPending = true;
    }

    private void LateUpdate()
    {
        if (!_contactPending) return;
        _contactPending = false;
        _contactCaptured = _ballmove != null && _swingIK != null && _swingIK.TryGetSweetSpot(out _contactSweetSpot);
        LogContact();
        // The pause is diagnostic: misses and missing-contact cases must remain visible too.
        if (_hitPauseSeconds > 0f)
            _pauseRoutine = StartCoroutine(PauseBeforeCalculation());
        else
        {
            RestoreTimeScale();
            CalculateNow();
        }
    }

    private void LogContact()
    {
        if (_parameters != null && !_parameters.EnableDebugLogs) return;
        Vector3 delta = _contactBallPosition - _contactSweetSpot;
        float limit = _parameters != null ? _parameters.MaxImpactDistance : 0f;
        float distance = delta.magnitude;
        string reason = _ballmove == null ? "NO_BALL" : !_contactCaptured ? "NO_IK_CONTACT" :
            _parameters == null ? "NO_PARAMETERS" : distance <= limit ? "HIT" :
            Mathf.Abs(delta.z) > limit ? "MISS_DEPTH_TIMING" :
            new Vector2(delta.x, delta.y).magnitude > limit ? "MISS_XY_AIM" : "MISS_COMBINED_DISTANCE";
        Debug.Log($"[Batting/Contact] result={reason} clipFrame={(_swingIK != null ? _swingIK.ContactClipFrame : -1f):F2} " +
            $"inputToEvent={Time.time - _swingStartedTime:F4}s rewind={_contactRewindSeconds * 1000f:F2}ms " +
            $"bat={(_contactCaptured ? _contactSweetSpot.ToString("F4") : "unavailable")} ball={(_ballmove != null ? _contactBallPosition.ToString("F4") : "null")} " +
            $"deltaXYZ={(_contactCaptured ? delta.ToString("F4") : "unavailable")} distance={(_contactCaptured ? distance.ToString("F4") : "unavailable")} limit={limit:F4} " +
            $"ballMoving={(_ballmove != null && _ballmove.IsMoving)} pause={_hitPauseSeconds:F2}s", this);
    }

    private void OnDrawGizmos()
    {
        if (!_pauseApplied || !_contactCaptured || _parameters == null || !_parameters.ShowTrajectoryGizmos) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(_contactSweetSpot, _parameters.MaxImpactDistance);
        Gizmos.DrawSphere(_contactSweetSpot, .02f);
        Gizmos.color = Color.red;
        Gizmos.DrawLine(_contactSweetSpot, _contactBallPosition);
        Gizmos.DrawWireSphere(_contactBallPosition, .035f);
    }

    public bool TryGetContact(PitchBallMove ball, out Vector3 sweetSpot, out Vector3 ballPosition)
    {
        sweetSpot = _contactSweetSpot;
        ballPosition = _contactBallPosition;
        return _contactCaptured && ball != null && ball == _ballmove;
    }

    private void CalculateNow()
    {
        Debug.Log("[BattingSystem] Batting calculation started.");
        _hitEvent?.RaiseEvent(_ballmove);
        _swingEvent?.RaiseEvent();
    }

    private System.Collections.IEnumerator PauseBeforeCalculation()
    {
        yield return new WaitForSecondsRealtime(_hitPauseSeconds);
        RestoreTimeScale();
        _pauseRoutine = null;
        CalculateNow();
    }

    private void EndHitPause()
    {
        _contactPending = false;
        _contactCaptured = false;
        if (_pauseRoutine != null) StopCoroutine(_pauseRoutine);
        RestoreTimeScale();
        _pauseRoutine = null;
    }

    private void RestoreTimeScale()
    {
        if (_pauseApplied) Time.timeScale = _timeScaleBeforePause;
        _pauseApplied = false;
    }

    /// <summary>
    /// ボールがリリースされたときの処理
    /// </summary>
    private void ReleasedBall(PitchBallMove ball)
    {
        Debug.Log("[BattingSystem] Ball Released - Swing is now allowed.");
        EndHitPause();
        _canSwing = true;
        _isSwinging = false;
        _impactSent = false;
        _ballmove = ball;
    }
}
