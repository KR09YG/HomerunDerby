using System;
using Unity.VisualScripting;
using UnityEngine;

[CreateAssetMenu(fileName = "OnBallLanded", menuName = "Scriptable Objects/OnBallLanded")]
public class OnBallLandedEvent : ScriptableObject
{
    private Action _onBallLanded;

    public void RegisterListener(Action listener)
    {
        _onBallLanded += listener;
    }

    public void UnregisterListener(Action listener)
    {
        _onBallLanded -= listener;
    }

    public void RaiseEvent()
    {
        _onBallLanded?.Invoke();
    }
}
