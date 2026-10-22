namespace Homerunderby
{
    using Cysharp.Threading.Tasks;
    using System.Collections.Generic;
    using UnityEngine;

    [System.Serializable]
    public class WeightedPitch
    {
        public PitchTypeData PitchType;
        public float Weight;
    }

    public class PitcherController : MonoBehaviour
    {
        [Header("Events")]
        [SerializeField] private OnStartPitchEvent _startPitchEvent;
        [SerializeField] private OnPitchBallReleaseEvent _releaseEvent;

        [SerializeField] private float _delayToNextPitch;
        [SerializeField] private PitcherAnimationController _animationController;
        [SerializeField] private BoxCollider _collider;
        [SerializeField] private PitchBallMove _ballMove;
        [SerializeField] private TrajectorySettings _trajectorySettings;
        [SerializeField] private BounceSettings _bounceSettings;
        [SerializeField] private Transform _releasePoint;
        [SerializeField] private Transform _passPoint;
        [SerializeField] private float _stopZ;

        private Vector3 _startPosition;
        private Vector3 _startEulerAngles;
        private BallData _selectedBall;
        private List<WeightedPitch> _ballList;
        private List<Vector3> _ballTrajectory;
        private bool _pitchPending;

        private void Awake()
        {
            _startPosition = transform.position;
            _startEulerAngles = transform.eulerAngles;

            if (_startPitchEvent != null) _startPitchEvent.RegisterListener(ToNextPitch);
            else Debug.LogError("OnStartPitchEvent が未設定");
        }
        private void OnDestroy()
        {
            _startPitchEvent?.UnregisterListener(ToNextPitch);
        }

        /// <summary>
        /// ステージで使用する球種を受け取り設定する
        /// </summary>
        public void SetBallList(List<WeightedPitch> ballList)
        {
            _ballList = ballList;
        }

        private void ToNextPitch()
        {
            if (_pitchPending) return;
            _pitchPending = true;
            WaitPitch().Forget();
        }

        private void ResetPos()
        {
            transform.position = _startPosition;
            transform.eulerAngles = _startEulerAngles;
        }

        private async UniTask WaitPitch()
        {
            try
            {
                await UniTask.WaitForSeconds(Mathf.Max(0f, _delayToNextPitch),
                    cancellationToken: this.GetCancellationTokenOnDestroy());
                ResetPos();
                _animationController.PlayPitch();
            }
            catch (System.OperationCanceledException)
            {
            }
            finally
            {
                _pitchPending = false;
            }
        }

        public void SelectPitch()
        {
            PitchTypeData pitch = PitchTypeSelector.Select(_ballList, Random.value);
            _passPoint.position = GetRandomPositionInCollider();
            _selectedBall = pitch.Data;
        }

        private Vector3 GetRandomPositionInCollider()
        {
            BoxCollider box = _collider;

            Vector3 localCenter = box.center;
            Vector3 localSize = box.size;

            float randomX = Random.Range(-localSize.x / 2, localSize.x / 2) + localCenter.x;
            float randomY = Random.Range(-localSize.y / 2, localSize.y / 2) + localCenter.y;
            float localZ = localCenter.z; // Zは中心固定

            Vector3 localPos = new Vector3(randomX, randomY, localZ);

            return box.transform.TransformPoint(localPos);
        }

        public void ReleasedBall()
        {
            if (_ballMove == null)
            {
                Debug.LogError("PitchBallMoveコンポーネントがアタッチされていません", this);
                return;
            }

            SelectPitch();
            _ballTrajectory = CalculateTrajectory(_selectedBall);
            if (_ballTrajectory == null || _ballTrajectory.Count < 2)
            {
                Debug.LogError("投球軌道を計算できませんでした。", this);
                return;
            }
            _ballMove.gameObject.transform.parent = null;

            _ballMove.Setup(
                trajectory: _ballTrajectory,
                deltaTime: _trajectorySettings.DeltaTime,
                spin: _pitchSpin
            );
            _releaseEvent?.RaiseEvent(_ballMove);
        }

        private SpinState _pitchSpin;

        private List<Vector3> CalculateTrajectory(BallData ball)
        {
            return BallPhysicsCalculator.CalculateTrajectory(new PitchRequest()
            {
                BallData = ball,
                ReleasePoint = _releasePoint.position,
                PassPoint = _passPoint.position,
                StopZ = _stopZ,
                Settings = _trajectorySettings,
                BounceSettings = _bounceSettings,
            }, out _pitchSpin);
        }
    }

}

