using UnityEngine;

public class BattingSystem : MonoBehaviour
{
    [SerializeField] private OnBattingInputEvent _inputEvent;
    [SerializeField] private OnSwingEvent _swingEvent;
    [SerializeField] private OnBallReachedTargetEvent _ballReachedTargetEvent;
    [SerializeField] private OnPitchBallReleaseEvent _releseEvent;
    [SerializeField] private OnBattingHitEvent _hitEvent;
    [SerializeField] private PitchBallMove _ballmove;
    private bool _canSwing = true;
    private bool _isSwinging = false;

    private void Awake()
    {
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

    }

    private void Update()
    {
        if (_canSwing && Input.GetMouseButtonDown(0) && !_isSwinging)
        {
            _isSwinging = true;
            _canSwing = false;
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
        Debug.Log("[BattingSystem] Batting calculation started.");
        _hitEvent?.RaiseEvent(_ballmove);
        _swingEvent.RaiseEvent();
    }

    /// <summary>
    /// ボールがリリースされたときの処理
    /// </summary>
    private void ReleasedBall(PitchBallMove ball)
    {
        Debug.Log("[BattingSystem] Ball Released - Swing is now allowed.");
        _canSwing = true;
        _isSwinging = false;
    }
}
