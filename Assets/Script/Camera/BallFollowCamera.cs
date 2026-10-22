using System.Collections;
using Cinemachine;
using UnityEngine;

public class BallFollowCamera : MonoBehaviour
{
    [Header("Events")]
    [SerializeField] private OnBattingResultEvent _battingResultEvent;
    [SerializeField] private OnAtBatResetEvent _atBatResetEvent;

    [Header("VirtualCamera")]
    [SerializeField] private CinemachineVirtualCamera _batterCamera;
    [SerializeField] private CinemachineVirtualCamera _ballFollowCamera;

    [Header("Follow Target")]
    [SerializeField] private Transform _ball;

    [Header("Follow Pivot")]
    [SerializeField] private Transform _followPivot;

    [Header("Follow Distance")]
    [SerializeField] private float _followDistance = 0.7f;

    [Header("Hit Camera")]
    [SerializeField, Min(0f)] private float _followDelaySeconds = 1.5f;
    [SerializeField, Range(0.5f, 1f)] private float _homeRunZoomScale = 0.75f;
    [SerializeField, Min(0.01f)] private float _homeRunZoomInSeconds = 0.24f;
    [SerializeField, Min(0.01f)] private float _homeRunZoomOutSeconds = 0.32f;

    private BattingSystem _battingSystem;
    private Coroutine _cameraRoutine;
    private float _contactRealtime = float.NegativeInfinity;
    private float _batterFieldOfView;
    private bool _isFollowing;

    private void Awake()
    {
        if (_battingResultEvent != null) _battingResultEvent.RegisterListener(OnHit);
        else Debug.LogError("OnBattingResultEvent が未設定", this);
        if (_atBatResetEvent != null) _atBatResetEvent.RegisterListener(OnFinishedFollowing);
        else Debug.LogError("OnAtBatResetEvent が未設定", this);

        _battingSystem = FindFirstObjectByType<BattingSystem>();
        if (_battingSystem != null) _battingSystem.ContactCaptured += OnContactCaptured;
        if (_batterCamera != null) _batterFieldOfView = _batterCamera.m_Lens.FieldOfView;
    }

    private void OnDestroy()
    {
        _battingResultEvent?.UnregisterListener(OnHit);
        _atBatResetEvent?.UnregisterListener(OnFinishedFollowing);
        if (_battingSystem != null) _battingSystem.ContactCaptured -= OnContactCaptured;
        CancelCameraRoutine();
    }

    private void LateUpdate()
    {
        if (_isFollowing) FollowingBall();
    }

    private void OnContactCaptured()
    {
        if (_battingSystem.IsHitStopped)
            _contactRealtime = Time.realtimeSinceStartup;
    }

    private void OnHit(BattingBallResult result)
    {
        if (result == null || result.BallType == BattingBallType.Miss) return;
        CancelCameraRoutine();
        _isFollowing = false;
        _ballFollowCamera.Priority = 0;
        _batterCamera.Priority = 10;
        float contactTime = float.IsNegativeInfinity(_contactRealtime)
            ? Time.realtimeSinceStartup : _contactRealtime;
        _cameraRoutine = StartCoroutine(SwitchAfterHit(result.BallType == BattingBallType.HomeRun, contactTime));
    }

    private IEnumerator SwitchAfterHit(bool homeRun, float contactTime)
    {
        if (homeRun && _batterCamera != null)
        {
            float zoomedFieldOfView = _batterFieldOfView * _homeRunZoomScale;
            yield return ChangeFieldOfView(_batterFieldOfView, zoomedFieldOfView, _homeRunZoomInSeconds);
            yield return ChangeFieldOfView(zoomedFieldOfView, _batterFieldOfView, _homeRunZoomOutSeconds);
        }

        float remaining = _followDelaySeconds - (Time.realtimeSinceStartup - contactTime);
        if (remaining > 0f) yield return new WaitForSecondsRealtime(remaining);

        _ballFollowCamera.LookAt = _ball;
        FollowingBall();
        _batterCamera.Priority = 0;
        _ballFollowCamera.Priority = 10;
        _isFollowing = true;
        _cameraRoutine = null;
    }

    private IEnumerator ChangeFieldOfView(float start, float end, float duration)
    {
        float elapsed = 0f;
        SetBatterFieldOfView(start);
        while (elapsed < duration)
        {
            yield return null;
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            SetBatterFieldOfView(Mathf.Lerp(start, end, progress));
        }
        SetBatterFieldOfView(end);
    }

    private void SetBatterFieldOfView(float fieldOfView)
    {
        LensSettings lens = _batterCamera.m_Lens;
        lens.FieldOfView = fieldOfView;
        _batterCamera.m_Lens = lens;
    }

    private void FollowingBall()
    {
        Vector3 distance = _ball.position - _followPivot.position;
        _ballFollowCamera.transform.position = _followPivot.position + distance * _followDistance;
    }

    public void OnFinishedFollowing()
    {
        CancelCameraRoutine();
        _contactRealtime = float.NegativeInfinity;
        _isFollowing = false;
        _ballFollowCamera.Priority = 0;
        _batterCamera.Priority = 10;
    }

    private void CancelCameraRoutine()
    {
        if (_cameraRoutine != null) StopCoroutine(_cameraRoutine);
        _cameraRoutine = null;
        if (_batterCamera != null) SetBatterFieldOfView(_batterFieldOfView);
    }
}
