using System;
using UnityEngine;

[CreateAssetMenu(fileName = "Ball", menuName = "Scriptable Objects/Ball")]
public class OnBallSpawnedEvent : ScriptableObject
{
    private Action<GameObject> _onBallSpawned;

    public void RaiseEvent(GameObject ball)
    {
        _onBallSpawned?.Invoke(ball);
    }

    public void RegisterListener(Action<GameObject> listener)
    {
        _onBallSpawned += listener;
    }

    public void UnregisterListener(Action<GameObject> listener)
    {
        _onBallSpawned -= listener;
    }
}
