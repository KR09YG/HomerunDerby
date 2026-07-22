using System;
using UnityEngine;

[CreateAssetMenu(fileName = "BallReachedTargetEvent", menuName = "Scriptable Objects/BallReachedTarget")]
public class OnBallReachedTargetEvent : ScriptableObject
{
    private Action<PitchBallMove> _reachedAction;

    public void RegisterListener(Action<PitchBallMove> listener)
    {
        _reachedAction += listener;
    }

    public void UnregisterListener(Action<PitchBallMove> listener)
    {
        _reachedAction -= listener;
    }

    public void RaiseEvent(PitchBallMove ball)
    {
        _reachedAction?.Invoke(ball);
    }
}
