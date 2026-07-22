using System.Collections.Generic;
using UnityEngine;

public static class BallTrajectoryPredictor
{
    public static bool TryGetCrossPointAtZ(
        IReadOnlyList<Vector3> trajectory,
        float targetZ,
        out Vector3 crossPoint)
    {
        crossPoint = default;

        if (trajectory == null || trajectory.Count < 2)
            return false;

        for (int i = 0; i < trajectory.Count - 1; i++)
        {
            Vector3 p1 = trajectory[i];
            Vector3 p2 = trajectory[i + 1];

            if ((p1.z <= targetZ && p2.z >= targetZ) ||
                (p1.z >= targetZ && p2.z <= targetZ))
            {
                float t = Mathf.InverseLerp(p1.z, p2.z, targetZ);
                crossPoint = Vector3.Lerp(p1, p2, t);
                return true;
            }
        }

        return false;
    }
}
