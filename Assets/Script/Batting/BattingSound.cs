using KanKikuchi.AudioManager;
using UnityEngine;

public class BattingSound : MonoBehaviour
{
    [SerializeField] private OnBattingResultEvent _resultEvent;
    [SerializeField] private float _sweetSpotVolumeRate = 1f;
    [SerializeField] private float _normalHitVolumeRate = 0.5f;

    private void OnEnable()
    {
        if (_resultEvent != null)
            _resultEvent.RegisterListener(OnResultReceived);
    }

    private void OnDisable()
    {
        if (_resultEvent != null)
            _resultEvent.UnregisterListener(OnResultReceived);
    }

    private void OnResultReceived(BattingBallResult result)
    {
        if (result.IsSweetSpot)
        {
            SEManager.Instance.Play(SEPath.BATTING_HIT,volumeRate: _sweetSpotVolumeRate);
        }
        else if (result.BallType != BattingBallType.Miss)
        {
            SEManager.Instance.Play(SEPath.BATTING_HIT,volumeRate: _normalHitVolumeRate);
        }
    }
}
