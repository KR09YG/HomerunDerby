using TMPro;
using UnityEngine;

public class BattingDistanceTracker : MonoBehaviour
{
    [SerializeField] private BattingBallMove _battingBallMove;
    [SerializeField] private OnAtBatResetEvent _onAtBatResetEvent;
    [SerializeField] private OnBattingResultEvent _onBattingResultEvent;
    [SerializeField] private OnBallLandedEvent _onBallLandedEvent;
    [SerializeField] private TextMeshProUGUI _distanceText;

    private Vector3 _startPos;
    private bool _isBallLanded;
    private float _distanceMeters;

    public float DistanceMeters => _distanceMeters;

    private void Awake()
    {
        if (_onAtBatResetEvent) _onAtBatResetEvent.RegisterListener(OnAtBatReset);
        else Debug.LogError("[BattingDistanceTracker] OnAtBatResetEvent is not assigned.", this);

        if (_onBattingResultEvent) _onBattingResultEvent.RegisterListener(OnBattingResult);
        else Debug.LogError("[BattingDistanceTracker] OnBattingResultEvent is not assigned.", this);

        if (_onBallLandedEvent) _onBallLandedEvent.RegisterListener(OnBallLanded);
        else Debug.LogError("[BattingDistanceTracker] OnBallLandedEvent is not assigned.", this);
    }

    private void OnDestroy()
    {
        _onAtBatResetEvent?.UnregisterListener(OnAtBatReset);
        _onBattingResultEvent?.UnregisterListener(OnBattingResult);
        _onBallLandedEvent?.UnregisterListener(OnBallLanded);
    }

    private void OnBattingResult(BattingBallResult result)
    {
        if (result.BallType != BattingBallType.Miss)
            Initialize(_battingBallMove.Position);
    }

    private void OnBallLanded()
    {
        if (_isBallLanded) return;
        _isBallLanded = true;
        if (_battingBallMove != null)
            UpdateDistancText(_battingBallMove.Position);
    }

    private void LateUpdate()
    {
        if (_battingBallMove == null)
        {
            Debug.LogError("[BattingDistanceTracker] BattingBallMove is not assigned.", this);
            return;
        }

        if (_battingBallMove.IsMoving && !_isBallLanded)
            UpdateDistancText(_battingBallMove.Position);
    }

    private void OnAtBatReset()
    {
        _distanceText.text = "";
    }

    public void Initialize(Vector3 startPos)
    {
        _startPos = startPos;
        _isBallLanded = false;
        _distanceMeters = 0f;
    }

    
    public float CalculateDistance(Vector3 ballPos, Vector3 StartPos)
    {
        float xDistance = ballPos.x - StartPos.x;
        float zDistance = ballPos.z - StartPos.z;
        return BaseballWorldScale.ToMeters(Mathf.Sqrt(xDistance * xDistance + zDistance * zDistance));
    }

    private void UpdateDistancText(Vector3 ballPos)
    {
        _distanceMeters = CalculateDistance(ballPos, _startPos);
        _distanceText.text = $"{_distanceMeters:F2} m";
    }
}
