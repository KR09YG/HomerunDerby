using System;
using UnityEngine;

[CreateAssetMenu(fileName = "SwingEvent", menuName = "Scriptable Objects/SwingEvent")]
public class OnSwingEvent : ScriptableObject
{
    private Action _swingEvent;

    public void RegisterListener(Action listener)
    {
        _swingEvent += listener;
    }

    public void UnregisterListener(Action listener)
    {
        _swingEvent -= listener;
    }

    public void RaiseEvent()
    {
        _swingEvent?.Invoke();
    }
}
