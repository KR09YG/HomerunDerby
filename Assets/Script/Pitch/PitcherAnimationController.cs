using UnityEngine;

namespace Homerunderby
{
    [RequireComponent(typeof(Animator))]
    public class PitcherAnimationController : MonoBehaviour
    {
        [SerializeField] private Animator _animator;

        public void PlayPitch()
        {
            _animator.Rebind();
            _animator.Play("Base Layer.Throwing", 0, 0f);
        }
    }
}
