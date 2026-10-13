using System;
using UnityEngine;

[CreateAssetMenu(fileName = "OnBatterReadyForPitchEvent", menuName = "Scriptable Objects/OnBatterReadyForPitchEvent")]
public class OnBatterReadyForPitchEvent : ScriptableObject
{
    private Action _onBatterReadyForPitchEvent;

    public void RegisterListener(Action listener)
    {
        _onBatterReadyForPitchEvent += listener;
    }

    public void UnregisterListener(Action listener)
    {
        _onBatterReadyForPitchEvent -= listener;
    }

    public void RaiseEvent()
    {
        _onBatterReadyForPitchEvent?.Invoke();
    }
}
