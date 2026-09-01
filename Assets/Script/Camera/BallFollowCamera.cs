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

    [Header("Follow Pivot")]
    [SerializeField] private Transform _followPivot;

    [Header("Follow Distance")]
    [SerializeField] private float _followDistance = 0.7f;

    private bool _isFollowing = false;

    private void Awake()
    {
        if (_battingResultEvent != null) _battingResultEvent.RegisterListener(OnHit);
        else Debug.LogError("OnBattingResultEvent が未設定");
        if (_atBatResetEvent != null) _atBatResetEvent.RegisterListener(OnFinishedFollowing);
        else Debug.LogError("OnAtBatResetEvent が未設定");
    }

    private void OnDestroy()
    {
        _battingResultEvent?.UnregisterListener(OnHit);
        _atBatResetEvent?.UnregisterListener(OnFinishedFollowing);
    }

    private void LateUpdate()
    {
        if (!_isFollowing) return;
        FollowingBall();
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
    private void FollowingBall()
    {
        if (!_isFollowing) return;
        _ballFollowCamera.transform.position = GetCameraPosition();

    }

    private Vector3 GetCameraPosition()
    {
        Vector3 distance = _ball.position - _followPivot.position;
        return _followPivot.position + distance * _followDistance;

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
    }
}