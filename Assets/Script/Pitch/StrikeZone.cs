using UnityEngine;

public sealed class StrikeZone : MonoBehaviour
{
    [SerializeField] private OnSwingEvent _swingEvent;
    [SerializeField] private OnAtBatResetEvent _atBatResetEvent;
    [SerializeField] private Renderer _renderer;
    public float CenterZ => transform.position.z;
    private void Awake()
    {
        if (_swingEvent != null) _swingEvent.RegisterListener(HideZone);
        else Debug.LogWarning("OnSwingEvent is not assigned in StrikeZone.");
        if (_atBatResetEvent != null) _atBatResetEvent.RegisterListener(ShowZone);
        else Debug.LogWarning("OnAtBatResetEvent is not assigned in StrikeZone.");
    }

    private void OnDestroy()
    {
        _swingEvent?.UnregisterListener(HideZone);
        _atBatResetEvent?.UnregisterListener(ShowZone);
    }

    private void HideZone()
    {
        Debug.Log("StrikeZone: HideZone called.");
        if (_renderer != null)
        {
            _renderer.enabled = false;
        }
    }
    private void ShowZone()
    {
        Debug.Log("StrikeZone: ShowZone called.");
        if (_renderer != null)
        {
            _renderer.enabled = true;
        }
    }
}
