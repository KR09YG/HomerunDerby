using System;
using System.Collections.Generic;
using UnityEngine;

public static class StadiumHomeRunJudge
{
    private const float FenceRayDistance = 500f;

    public static bool TryFindFenceCrossing(
        IReadOnlyList<Vector3> trajectory,
        int firstGroundIndex,
        Vector3 homePlate,
        Vector3 fieldForward,
        float fairAngleDegrees,
        out int crossingIndex)
    {
        crossingIndex = -1;
        if (trajectory == null || trajectory.Count < 2) return false;

        var panels = new List<MeshCollider>();
        float nearestPanelDistance = float.PositiveInfinity;
        foreach (MeshCollider collider in UnityEngine.Object.FindObjectsByType<MeshCollider>(FindObjectsSortMode.None))
        {
            if (!collider.gameObject.name.StartsWith("WALL_Panel_", StringComparison.Ordinal)) continue;
            panels.Add(collider);
            Vector3 offset = collider.bounds.ClosestPoint(homePlate) - homePlate;
            offset.y = 0f;
            nearestPanelDistance = Mathf.Min(nearestPanelDistance, offset.magnitude);
        }

        if (panels.Count == 0) return false;

        Vector3 forward = Vector3.ProjectOnPlane(fieldForward, Vector3.up).normalized;
        float ballRadius = BaseballWorldScale.ToWorldUnits(BallPhysicsConstants.BALL_RADIUS_M);
        int endIndex = firstGroundIndex >= 0 ? Mathf.Min(firstGroundIndex, trajectory.Count - 1) : trajectory.Count - 1;

        for (int i = 1; i <= endIndex; i++)
        {
            Vector3 from = trajectory[i - 1];
            Vector3 to = trajectory[i];
            Vector3 horizontal = Vector3.ProjectOnPlane(to - homePlate, Vector3.up);
            if (horizontal.magnitude < nearestPanelDistance - ballRadius) continue;

            Vector3 direction = horizontal.normalized;
            if (Vector3.Angle(forward, direction) > fairAngleDegrees) continue;

            float fenceDistance = float.PositiveInfinity;
            float fenceTop = 0f;
            foreach (MeshCollider panel in panels)
            {
                Vector3 rayOrigin = new Vector3(homePlate.x, panel.bounds.center.y, homePlate.z);
                if (!TryGetFenceDistance(panel, rayOrigin, direction, out float distance) ||
                    distance >= fenceDistance) continue;
                fenceDistance = distance;
                fenceTop = panel.bounds.max.y;
            }

            if (float.IsPositiveInfinity(fenceDistance)) continue;

            float fromDepth = Vector3.Dot(from - homePlate, direction);
            float toDepth = Vector3.Dot(to - homePlate, direction);
            // 中心がフェンス面を横切った区間で確定する。半径分の通過を待つと、
            // 次の区間では始点が面の外になり、越えた瞬間を取りこぼす。
            if (fromDepth >= fenceDistance || toDepth < fenceDistance) continue;

            float fraction = (fenceDistance - fromDepth) / (toDepth - fromDepth);
            float height = Mathf.Lerp(from.y, to.y, fraction);
            if (height < fenceTop + ballRadius) continue;

            crossingIndex = i;
            return true;
        }

        return false;
    }

    private static bool TryGetFenceDistance(MeshCollider panel, Vector3 origin, Vector3 direction, out float distance)
    {
        if (panel.Raycast(new Ray(origin, direction), out RaycastHit hit, FenceRayDistance))
        {
            distance = hit.distance;
            return true;
        }

        Vector3 outside = origin + direction * FenceRayDistance;
        if (panel.Raycast(new Ray(outside, -direction), out hit, FenceRayDistance))
        {
            distance = FenceRayDistance - hit.distance;
            return true;
        }

        return panel.bounds.IntersectRay(new Ray(origin, direction), out distance) &&
               distance >= 0f && distance < FenceRayDistance;
    }
}
