using System;
using System.Collections.Generic;
using UnityEngine;

namespace Homerunderby
{
    public sealed class PitchTestBallPlayer : MonoBehaviour
    {
        private IReadOnlyList<Vector3> _trajectory;
        private float _deltaTime;
        private float _elapsed;
        private SpinState _spin;
        private Quaternion _releaseRotation;
        public bool IsPlaying { get; private set; }
        public float PlaybackSpeed { get; set; } = 1f;

        public void Play(IReadOnlyList<Vector3> trajectory, float deltaTime, SpinState spin)
        {
            if (trajectory == null || trajectory.Count < 2 || deltaTime <= 0)
                throw new ArgumentException("再生する軌道には2点以上と正の時間刻みが必要です。");
            _trajectory = trajectory;
            _deltaTime = deltaTime;
            _elapsed = 0;
            _spin = spin;
            _releaseRotation = transform.rotation;
            transform.position = trajectory[0];
            IsPlaying = true;
        }

        public void ResetAt(Transform releasePoint)
        {
            IsPlaying = false;
            _trajectory = null;
            transform.SetPositionAndRotation(releasePoint.position, releasePoint.rotation);
        }

        private void Update() => Advance(Time.deltaTime * PlaybackSpeed);

        internal void Advance(float seconds)
        {
            if (!IsPlaying) return;
            float duration = (_trajectory.Count - 1) * _deltaTime;
            float nextElapsed = _elapsed + Mathf.Max(0, seconds);
            bool completed = nextElapsed >= duration;
            _elapsed = Mathf.Min(nextElapsed, duration);
            int index = Mathf.Min(Mathf.FloorToInt(_elapsed / _deltaTime), _trajectory.Count - 2);
            float t = (_elapsed - index * _deltaTime) / _deltaTime;
            transform.position = Vector3.Lerp(_trajectory[index], _trajectory[index + 1], t);
            transform.rotation = _spin.RotationAfter(_elapsed) * _releaseRotation;
            if (completed) IsPlaying = false;
        }
    }
}
