using System.Collections.Generic;
using UnityEngine;

namespace Homerunderby
{
    public static class PitchTestTrajectoryComparison
    {
        public readonly struct Result
        {
            public List<Vector3> Trajectory { get; }
            public List<Vector3> ReferenceTrajectory { get; }
            public Vector3 InitialVelocityMps { get; }
            public Vector3 ReferenceVelocityMps { get; }
            public SpinState Spin { get; }

            internal Result(List<Vector3> trajectory, List<Vector3> referenceTrajectory,
                Vector3 initialVelocityMps, Vector3 referenceVelocityMps, SpinState spin)
            {
                Trajectory = trajectory;
                ReferenceTrajectory = referenceTrajectory;
                InitialVelocityMps = initialVelocityMps;
                ReferenceVelocityMps = referenceVelocityMps;
                Spin = spin;
            }
        }

        public static Result Calculate(PitchRequest request)
        {
            var settings = request.Settings ?? new TrajectorySettings();
            var trajectory = BallPhysicsCalculator.CalculateTrajectory(request, out var spin, out var velocity);
            var config = new BallPhysicsCalculator.SimulationConfig {
                DeltaTime = settings.DeltaTime, MaxSimulationTimeSeconds = settings.MaxSimulationTime,
                StopAtZ = request.StopZ, BounceSettings = request.BounceSettings,
                PitchSpin = SpinState.FromAngularVelocity(Vector3.zero) };

            // 初速を揃えて回転だけを取り除き、回転による軌道の差を調べる。
            var referenceTrajectory = BallPhysicsCalculator.SimulateTrajectory(request.ReleasePoint,
                velocity, Vector3.zero, 0, 0, config);
            return new Result(trajectory, referenceTrajectory, velocity, velocity, spin);
        }
    }
}
