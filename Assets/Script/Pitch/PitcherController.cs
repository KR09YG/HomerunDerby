namespace Homerunderby
{
    using Cysharp.Threading.Tasks;
    using System.Collections.Generic;
    using UnityEngine;

    public class PitcherController : MonoBehaviour
    {
        [Header("Events")]
        [SerializeField] private OnStartPitchEvent _startPitchEvent;
        [SerializeField] private OnPitchBallReleaseEvent _releaseEvent;

        [SerializeField] private PitcherAnimationController _animationController;
        [SerializeField] private BoxCollider _collider;
        [SerializeField] LineRenderer _lineRenderer;
        [SerializeField] private PitchBallMove _ballMove;
        [SerializeField] private int _updateInterval = 5; // 何フレームに1回更新するか
        [SerializeField] private TrajectorySettings _trajectorySettings;
        [SerializeField] private BounceSettings _bounceSettings;
        [SerializeField] private int _ballIndex = -1;
        [SerializeField] private Transform _releasePoint;
        [SerializeField] private Transform _passPoint;
        [SerializeField] private float _stopZ;
        [SerializeField] private List<BallData> _ballList;
        [SerializeField] private float _nodAnimTime;
        [SerializeField] private int _delayFirstPitchTime = 5000;

        private Vector3 _startPos;
        private Vector3 _startRota;
        public BallData _selectedBall;
        private List<Vector3> _ballTrajectory = new List<Vector3>();
        public bool _isPlaying = false;
        public List<BallData> BallList => _ballList;

        public void Awake()
        {
            _startPos = transform.position;
            _startRota = transform.eulerAngles;

            if (_startPitchEvent != null) _startPitchEvent.RegisterListener(ToNextPitch);
            else Debug.LogError("OnStartPitchEvent が未設定");
        }
        private void OnDestroy()
        {
            _startPitchEvent?.UnregisterListener(ToNextPitch);
        }

        private void Start()
        {
            WaitForFirstPitch().Forget();
        }

        private async UniTaskVoid WaitForFirstPitch()
        {
            await UniTask.Delay(_delayFirstPitchTime);
            WaitPitch().Forget();
        }

        private void ToNextPitch()
        {
            _animationController.SetTrigger(PitcherAnimationState.ToIdle);
            WaitPitch().Forget();
        }

        private void ResetPos()
        {
            transform.position = _startPos;
            transform.eulerAngles = _startRota; 
        }

        private async UniTaskVoid WaitPitch()
        {
            await UniTask.Delay(2000);
            ResetPos();
            _animationController.SetTrigger(PitcherAnimationState.ToThrow);
            _animationController.SetTrigger(PitcherAnimationState.Throw);
        }

        public void PitchCalculate()
        {
            int index = Random.Range( 0, _ballList.Count );
            _passPoint.position = GetRandomPositionInCollider();
            _selectedBall = _ballList[index];
            Debug.Log($"Ball decided: {_selectedBall.Name}");
        }

        // 投球位置をコライダー内のランダムな位置に設定
        private Vector3 GetRandomPositionInCollider()
        {
            BoxCollider box = _collider as BoxCollider;

            // ローカル空間でのランダム座標(X, Y)
            Vector3 localCenter = box.center;
            Vector3 localSize = box.size;

            float randomX = Random.Range(-localSize.x / 2, localSize.x / 2) + localCenter.x;
            float randomY = Random.Range(-localSize.y / 2, localSize.y / 2) + localCenter.y;
            float localZ = localCenter.z; // Zは中心固定

            Vector3 localPos = new Vector3(randomX, randomY, localZ);

            // ローカル → ワールドへ変換(回転・スケール・位置を反映)
            return box.transform.TransformPoint(localPos);
        }

        public void ReleasedBall()
        {
            _releaseEvent?.RaiseEvent(_ballMove);
            PitchCalculate();
            _ballTrajectory = CalculatedBallTrajectry(_selectedBall);
            if (!_ballMove)
            {
                Debug.LogError("PitchBallMoveコンポーネントがアタッチされていません");
                return;
            }
            Vector3 spinAxis = BallPhysicsCalculator.ToSpinAxis(
                _selectedBall.SpinTilt,
                _selectedBall.SpinEfficiency
            );

            _ballMove.gameObject.transform.parent = null;

            _ballMove.Setup(
                trajectory: _ballTrajectory,
                deltaTime: _trajectorySettings.DeltaTime,
                spinAxis: spinAxis,
                spinRate: _selectedBall.RotateSpeed
            );
        }

        /// <summary>
        /// ボールの軌道を計算
        /// </summary>
        /// <param name="ball"></param>
        /// <returns></returns>
        public List<Vector3> CalculatedBallTrajectry(BallData ball)
        {
            return BallPhysicsCalculator.CalculateTrajectory(new PitchRequest()
            {
                BallData = ball,
                ReleasePoint = _releasePoint.position,
                PassPoint = _passPoint.position,
                StopZ = _stopZ,
                Settings = _trajectorySettings,
                BounceSettings = _bounceSettings,
            });
        }
    }

}

