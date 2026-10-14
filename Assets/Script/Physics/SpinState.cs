using System;
using UnityEngine;

/// <summary>
/// リリース時の総角速度をワールド座標で保持する。飛翔中の回転減衰と空力トルクは省略する。
/// </summary>
public readonly struct SpinState
{
    public Vector3 AngularVelocityRadPerSec { get; }
    public float TotalSpinRateRpm => AngularVelocityRadPerSec.magnitude / BallPhysicsConstants.RPM_TO_RAD_PER_SEC;

    private SpinState(Vector3 angularVelocityRadPerSec)
    {
        AngularVelocityRadPerSec = angularVelocityRadPerSec;
    }

    public static SpinState Create(Vector3 spinAxis, float totalSpinRateRpm)
    {
        if (float.IsNaN(totalSpinRateRpm) || float.IsInfinity(totalSpinRateRpm) || totalSpinRateRpm < 0f)
            throw new ArgumentOutOfRangeException(nameof(totalSpinRateRpm));
        float lengthSquared = spinAxis.sqrMagnitude;
        if (float.IsNaN(lengthSquared) || float.IsInfinity(lengthSquared))
            throw new ArgumentOutOfRangeException(nameof(spinAxis));
        if (totalSpinRateRpm == 0f) return new SpinState(Vector3.zero);
        if (lengthSquared < 1e-12f)
            throw new ArgumentException("回転数が正の場合は非ゼロの回転軸を指定してください。", nameof(spinAxis));
        return FromAngularVelocity((spinAxis / Mathf.Sqrt(lengthSquared)) * (totalSpinRateRpm * BallPhysicsConstants.RPM_TO_RAD_PER_SEC));
    }

    // 飛翔中に速度の向きが変わるため、この割合も一定にはならない。
    public float GetTransverseRatio(Vector3 velocityMps)
    {
        float angularSpeed = AngularVelocityRadPerSec.magnitude;
        return angularSpeed > 0f ? Mathf.Clamp01(GetTransverseAngularVelocity(velocityMps).magnitude / angularSpeed) : 0f;
    }

    public float GetSignedGyroRatio(Vector3 velocityMps)
    {
        float angularSpeed = AngularVelocityRadPerSec.magnitude;
        return angularSpeed > 0f ? Mathf.Clamp(Vector3.Dot(AngularVelocityRadPerSec / angularSpeed, velocityMps.normalized), -1f, 1f) : 0f;
    }

    public static SpinState FromAngularVelocity(Vector3 angularVelocityRadPerSec)
    {
        if (float.IsNaN(angularVelocityRadPerSec.sqrMagnitude) || float.IsInfinity(angularVelocityRadPerSec.sqrMagnitude))
            throw new ArgumentOutOfRangeException(nameof(angularVelocityRadPerSec));
        return new SpinState(angularVelocityRadPerSec);
    }

    public Vector3 GetTransverseAngularVelocity(Vector3 velocityMps)
    {
        if (velocityMps.sqrMagnitude < 1e-12f) return Vector3.zero;
        Vector3 forward = velocityMps.normalized;
        return AngularVelocityRadPerSec - Vector3.Dot(AngularVelocityRadPerSec, forward) * forward;
    }

    public Quaternion RotationAfter(float seconds)
    {
        float angularSpeedRadPerSec = AngularVelocityRadPerSec.magnitude;
        if (angularSpeedRadPerSec < 1e-8f) return Quaternion.identity;
        return Quaternion.AngleAxis(angularSpeedRadPerSec * Mathf.Rad2Deg * seconds, AngularVelocityRadPerSec / angularSpeedRadPerSec);
    }
}
