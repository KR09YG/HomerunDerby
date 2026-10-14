using UnityEngine;

namespace Homerunderby
{
    public sealed class PitchTestReleaseReceiver : MonoBehaviour
    {
        [SerializeField] private PitchTestSceneController _controller;

        public void ReleasedBall() => _controller.QueueRelease();

#if UNITY_EDITOR
        public void Configure(PitchTestSceneController controller) => _controller = controller;
#endif
    }
}
