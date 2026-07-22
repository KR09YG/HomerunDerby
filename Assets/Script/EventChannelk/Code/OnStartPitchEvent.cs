using System;
using UnityEngine;

[CreateAssetMenu(fileName = "StartPitch", menuName = "Scriptable Objects/StartPitch")]
public class OnStartPitchEvent : ScriptableObject
{
    private Action _startPitchEvent;

    public void RegisterListener(Action listener)
    {
        _startPitchEvent += listener;
    }

    public void UnregisterListener(Action listener)
    {
        _startPitchEvent -= listener;
    }

    public void RaiseEvent()
    {
        _startPitchEvent?.Invoke();
    }
}
