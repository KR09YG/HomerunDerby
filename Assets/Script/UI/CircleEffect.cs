using UnityEngine;

public class CircleEffect : MonoBehaviour
{
    [SerializeField] private Transform _ball;
    [SerializeField] private OnBattingHitEvent _battingHitEvent;
    [SerializeField] private OnAtBatResetEvent _atBatResetEvent;

    private void Awake()
    {
        Hide();
        if (_battingHitEvent != null) _battingHitEvent.RegisterListener(Show);
        else Debug.LogError("BattingHitEvent が設定されていません");
        if (_atBatResetEvent != null) _atBatResetEvent.RegisterListener(Hide);
        else Debug.LogError("AtBatResetEvent が設定されていません");
    }

    private void OnDestroy()
    {
        _battingHitEvent?.UnregisterListener(Show);
        _atBatResetEvent?.UnregisterListener(Hide);
    }

    private void Update()
    {
        if (_ball != null)
        { 
            // 子オブジェクトにすると回転してしまうため
            transform.position = _ball.position;
        }
    }

    public void Show(PitchBallMove ball)
    {
        gameObject.SetActive(true);
    }
    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
