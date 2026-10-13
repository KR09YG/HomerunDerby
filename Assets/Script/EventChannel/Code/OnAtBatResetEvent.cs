using System;
using UnityEngine;

[CreateAssetMenu(fileName = "OnAtBatResetEvent", menuName = "Scriptable Objects/OnAtBatResetEvent")]
public class OnAtBatResetEvent : ScriptableObject
{
    private Action _atBatReset;

    public void RegisterListener(Action listener)
    {
        _atBatReset += listener;
    }

    public void UnregisterListener(Action listener)
    {
        _atBatReset -= listener;
    }

    public void RaiseEvent()
    {
        _atBatReset?.Invoke();
    }
}
