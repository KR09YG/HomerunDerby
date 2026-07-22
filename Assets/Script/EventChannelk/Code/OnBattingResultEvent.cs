using System;
using UnityEngine;

[CreateAssetMenu(fileName = "BattingResultEvent", menuName = "Scriptable Objects/BattingResultEvent")]
public class OnBattingResultEvent : ScriptableObject
{
    private Action<BattingBallResult> _battingResultAction;

    public void RegisterListener(Action<BattingBallResult> listener)
    {
        _battingResultAction += listener;
    }

    public void UnregisterListener(Action<BattingBallResult> listener)
    {
        _battingResultAction -= listener;
    }

    public void RaiseEvent(BattingBallResult result)
    {
        _battingResultAction?.Invoke(result);
    }
}
