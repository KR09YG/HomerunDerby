using System.Collections.Generic;
using UnityEngine;

public readonly struct FairFoulDecision
{
    public readonly bool IsFoul;
    public readonly int ResolutionIndex;
    public readonly Vector3 Position;
    public readonly float AngleDegrees;

    public FairFoulDecision(bool isFoul, int resolutionIndex, Vector3 position, float angleDegrees)
    {
        IsFoul = isFoul;
        ResolutionIndex = resolutionIndex;
        Position = position;
        AngleDegrees = angleDegrees;
    }
}

public static class FairFoulJudge
{
    public static FairFoulDecision Evaluate(
        IReadOnlyList<Vector3> trajectory,
        int firstGroundIndex,
        Vector3 homePlate,
        Vector3 fieldForward,
        float fairAngleDegrees,
        float baseDistanceWorldUnits)
    {
        Vector3 forward = Vector3.ProjectOnPlane(fieldForward, Vector3.up).normalized;
        Vector3 side = Vector3.Cross(Vector3.up, forward);
        float baseDepth = baseDistanceWorldUnits * Mathf.Cos(fairAngleDegrees * Mathf.Deg2Rad);
        int lastIndex = trajectory.Count - 1;
        int decisionIndex = firstGroundIndex >= 0 && firstGroundIndex <= lastIndex
            ? firstGroundIndex : lastIndex;
        Vector3 position = trajectory[decisionIndex];

        // 塁より手前で落ちた打球は、塁を越えた位置か停止位置で決める。
        if (firstGroundIndex >= 0 && Depth(position, homePlate, forward) < baseDepth)
        {
            for (int i = firstGroundIndex + 1; i <= lastIndex; i++)
            {
                float previousDepth = Depth(trajectory[i - 1], homePlate, forward);
                float currentDepth = Depth(trajectory[i], homePlate, forward);
                if (previousDepth < baseDepth && currentDepth >= baseDepth)
                {
                    float t = (baseDepth - previousDepth) / (currentDepth - previousDepth);
                    position = Vector3.Lerp(trajectory[i - 1], trajectory[i], t);
                    decisionIndex = i;
                    break;
                }
            }

            if (decisionIndex == firstGroundIndex)
            {
                decisionIndex = lastIndex;
                position = trajectory[lastIndex];
            }
        }

        Vector3 offset = position - homePlate;
        float depth = Vector3.Dot(offset, forward);
        float lateral = Vector3.Dot(offset, side);
        float angle = Mathf.Atan2(lateral, depth) * Mathf.Rad2Deg;
        bool isFoul = depth < 0f || Mathf.Abs(lateral) >
            depth * Mathf.Tan(fairAngleDegrees * Mathf.Deg2Rad);
        return new FairFoulDecision(isFoul, decisionIndex, position, angle);
    }

    private static float Depth(Vector3 point, Vector3 homePlate, Vector3 forward)
    {
        return Vector3.Dot(point - homePlate, forward);
    }
}
