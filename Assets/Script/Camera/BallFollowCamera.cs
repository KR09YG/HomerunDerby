using Cinemachine;
using Cysharp.Threading.Tasks;
using System.Threading.Tasks;
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

    [Header("FOV設定")]
    [SerializeField] private float _fovDefault = 60f;
    [SerializeField] private float _fovZoomMin = 25f;
    [SerializeField] private float _zoomDistanceMax = 80f;
    [SerializeField] private float _zoomDistanceMin = 10f;
    [SerializeField, Range(1f, 20f)] private float _fovSpeed = 5f;

    private bool _isFollowing = false;
    private CinemachineComposer _composer;

    private void Awake()
    {
        if (_battingResultEvent != null) _battingResultEvent.RegisterListener(OnHit);
        else Debug.LogError("OnBattingResultEvent が未設定");
        if (_atBatResetEvent != null) _atBatResetEvent.RegisterListener(OnFinishedFollowing);
        else Debug.LogError("OnAtBatResetEvent が未設定");

        _composer = _ballFollowCamera.GetCinemachineComponent<CinemachineComposer>();

        // 初期FOV
        var lens = _ballFollowCamera.m_Lens;
        lens.FieldOfView = _fovDefault;
        _ballFollowCamera.m_Lens = lens;
    }

    private void OnDestroy()
    {
        _battingResultEvent?.UnregisterListener(OnHit);
        _atBatResetEvent?.UnregisterListener(OnFinishedFollowing);
    }

    private void LateUpdate()
    {
        if (!_isFollowing) return;
        UpdateFov();
    }

    private void UpdateFov()
    {
        float dist = Vector3.Distance(_ballFollowCamera.transform.position, _ball.position);
        float t = Mathf.InverseLerp(_zoomDistanceMin, _zoomDistanceMax, dist);
        float targetFov = Mathf.Lerp(_fovZoomMin, _fovDefault, t);

        var lens = _ballFollowCamera.m_Lens;
        lens.FieldOfView = Mathf.Lerp(lens.FieldOfView, targetFov, _fovSpeed * Time.deltaTime);
        _ballFollowCamera.m_Lens = lens;
    }

    private void OnHit(BattingBallResult result)
    {
        if (result.BallType == BattingBallType.Miss) return;

        // ComposerのLookAtにボールをセット
        _ballFollowCamera.LookAt = _ball;

        _batterCamera.Priority = 0;
        _ballFollowCamera.Priority = 10;
        _isFollowing = true;
    }

    public void OnFinishedFollowing()
    {
        _isFollowing = false;

        WaitCameraChange().Forget();
    }

    private async UniTaskVoid WaitCameraChange()
    {
        await UniTask.Delay(1000);
        _ballFollowCamera.Priority = 0;
        _batterCamera.Priority = 10;

        var lens = _ballFollowCamera.m_Lens;
        lens.FieldOfView = _fovDefault;
        _ballFollowCamera.m_Lens = lens;
    }
}