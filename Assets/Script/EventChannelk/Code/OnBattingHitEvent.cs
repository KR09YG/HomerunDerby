using System;
using UnityEngine;

[CreateAssetMenu(fileName = "BattingContactEvent", menuName = "Scriptable Objects/BattingContactEvent")]
public class OnBattingHitEvent : ScriptableObject
{
    private Action<PitchBallMove> _onHitAttempt;

    public void RegisterListener(Action<PitchBallMove> callback)
    {
        _onHitAttempt += callback;
    }

    public void UnregisterListener(Action<PitchBallMove> callback)
    {
        _onHitAttempt -= callback;
    }

    public void RaiseEvent(PitchBallMove data)
    {
        _onHitAttempt?.Invoke(data);
    }
}
