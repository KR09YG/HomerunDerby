namespace Homerunderby
{
    using UnityEngine;

    public enum PitcherAnimationState
    {
        ToThrow,
        Throw,
        ShakeHead,
        NodHead,
        ToIdle
    }

    [RequireComponent(typeof(Animator))]
    public class PitcherAnimationController : MonoBehaviour
    {
        [SerializeField] private Animator _animator;
        [SerializeField] private Transform _ballReleasePoint;
        public PitcherAnimationState CurrentState { get; private set; }
        public void SetTrigger(PitcherAnimationState animState)
        {
            CurrentState = animState;
            Debug.Log($"PitcherAnimationController: SetTrigger {animState}");
            _animator.SetTrigger(animState.ToString());
        }

        public void Log()
        {
            Debug.Log(_ballReleasePoint.position);
        }
    }
}
