using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Homerunderby
{
    public sealed class PitchTestSceneController : MonoBehaviour
    {
        private const float LaunchAngleStepDegrees = 0.1f;
        private const float MaxLaunchAngleDegrees = 45f;
        private const float ReleaseEventGraceSeconds = 0.5f;
        [Serializable]
        public sealed class View
        {
            public string Name;
            public Camera Camera;
        }

        [Header("投球")]
        [SerializeField] private PitchTypeData[] _pitchTypes;
        [SerializeField] private Transform _pitcherRoot;
        [SerializeField] private Animator _pitcherAnimator;
        [SerializeField] private Transform _releasePoint;
        [SerializeField] private BoxCollider _strikeZone;
        [SerializeField] private PitchTestBallPlayer _ball;
        [SerializeField] private TrajectorySettings _settings = new TrajectorySettings();
        [SerializeField] private float _stopZ = 2.07f;
        [SerializeField] private float _pitchAnimationSeconds = 6f;
        [SerializeField] private float _releaseEventSeconds = 3f;
        [SerializeField] private PitchLaunchMode _launchMode = PitchLaunchMode.TargetPoint;
        [Tooltip("方向指定時の角度（度）。Xは+Zから+Xへ向く角度、Yは仰角。I/K/J/Lキーでも調整できる。")]
        [SerializeField] private Vector2 _launchAnglesDegrees;
        [Header("表示")]
        [SerializeField] private View[] _views;
        [SerializeField] private LineRenderer _trajectoryLine;
        [SerializeField] private LineRenderer _referenceTrajectoryLine;
        [SerializeField] private TMP_Text _comparisonText;
        [SerializeField] private Toggle _comparisonToggle;
        [SerializeField] private Button _closeViewButton;
        [SerializeField] private int _closeSideViewIndex = 6;
        [SerializeField] private Transform _hoverMarker;
        [SerializeField] private Transform _targetMarker;
        [SerializeField] private Transform _crossMarker;
        [Header("操作パネル")]
        [SerializeField] private TMP_Dropdown _pitchDropdown;
        [SerializeField] private TMP_Text _statusText;
        [SerializeField] private TMP_Text _cameraText;
        [SerializeField] private RectTransform _targetMap;
        [SerializeField] private RectTransform _mapHoverMarker;
        [SerializeField] private RectTransform _mapTargetMarker;
        [SerializeField] private RectTransform _mapCrossMarker;
        [SerializeField] private Button _repeatButton;
        [SerializeField] private Button _resetButton;
        [SerializeField] private Button _previousCameraButton;
        [SerializeField] private Button _nextCameraButton;
        [SerializeField] private Toggle _trajectoryToggle;
        [SerializeField] private Slider _playbackSlider;

        private enum State { Ready, Windup, Flight, Recovery }
        private State _state;
        private BallData _queuedPitch;
        private PitchLaunchMode _queuedLaunchMode;
        private Vector3 _queuedLaunchDirection;
        private Vector3 _target, _hover, _rootPosition;
        private Quaternion _rootRotation;
        private int _viewIndex;
        private bool _releaseQueued, _hasTarget, _hasCross, _cursorWasVisible;
        private float _animationElapsed;
        private Vector2 _lastMousePosition;
        private readonly List<RaycastResult> _uiHits = new List<RaycastResult>();
        public Vector3 TargetPoint => _target;
        public Camera ActiveCamera => _views[_viewIndex].Camera;
        public bool IsBusy => _state != State.Ready;

        private void Start()
        {
            RefreshPitchOptions();
            EnsureComparisonReferences();
            ConfigurePhysicsLabels();
            _cursorWasVisible = Cursor.visible;
            Cursor.visible = true;
            // 矢印キーとSpaceはテスト操作に使う。UIはマウスで選択する。
            if (EventSystem.current != null) EventSystem.current.sendNavigationEvents = false;
            _rootPosition = _pitcherRoot.position;
            _rootRotation = _pitcherRoot.rotation;
            _target = _hover = _strikeZone.bounds.center;
            _lastMousePosition = Input.mousePosition;
            _repeatButton.onClick.AddListener(RepeatPitch);
            _resetButton.onClick.AddListener(ResetPitch);
            _previousCameraButton.onClick.AddListener(() => ChangeCamera(-1));
            _nextCameraButton.onClick.AddListener(() => ChangeCamera(1));
            _trajectoryToggle.onValueChanged.AddListener(SetTrajectoryVisible);
            if (_comparisonToggle != null) _comparisonToggle.onValueChanged.AddListener(SetComparisonVisible);
            if (_closeViewButton != null) _closeViewButton.onClick.AddListener(ShowCloseView);
            _playbackSlider.onValueChanged.AddListener(SetPlaybackSpeed);
            SetPlaybackSpeed(_playbackSlider.value);
            ChangeCamera(0);
            ResetPitch();
        }

        public void RefreshPitchOptions()
        {
            if (_pitchDropdown == null) return;
            int selected = _pitchDropdown.value;
            var options = new List<string>();
            if (_pitchTypes != null)
                foreach (var pitch in _pitchTypes)
                    options.Add(pitch != null ? $"{pitch.Data.Name}  {pitch.Data.Speed:F1} km/h" : "球種が未設定");
            _pitchDropdown.ClearOptions();
            _pitchDropdown.AddOptions(options);
            _pitchDropdown.SetValueWithoutNotify(Mathf.Clamp(selected, 0, Mathf.Max(0, options.Count - 1)));
        }

        private void OnDestroy()
        {
            Cursor.visible = _cursorWasVisible;
        }

        private void Update()
        {
            bool switched = false;
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.DownArrow))
            { ChangeCamera(-1); switched = true; }
            else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.UpArrow))
            { ChangeCamera(1); switched = true; }
            if (Input.GetKeyDown(KeyCode.R)) ResetPitch();
            if (Input.GetKeyDown(KeyCode.Space)) RepeatPitch();
            if (Input.GetKeyDown(KeyCode.F)) { ShowCloseView(); switched = true; }
            if (!IsBusy)
            {
                if (Input.GetKeyDown(KeyCode.T))
                {
                    _launchMode = _launchMode == PitchLaunchMode.Direction ? PitchLaunchMode.TargetPoint : PitchLaunchMode.Direction;
                    ResetPitch();
                }
                if (_launchMode == PitchLaunchMode.Direction)
                {
                    Vector2 change = Vector2.zero;
                    if (Input.GetKeyDown(KeyCode.I)) change.y += LaunchAngleStepDegrees;
                    if (Input.GetKeyDown(KeyCode.K)) change.y -= LaunchAngleStepDegrees;
                    if (Input.GetKeyDown(KeyCode.J)) change.x -= LaunchAngleStepDegrees;
                    if (Input.GetKeyDown(KeyCode.L)) change.x += LaunchAngleStepDegrees;
                    if (change != Vector2.zero)
                    {
                        _launchAnglesDegrees += change;
                        _launchAnglesDegrees.x = Mathf.Clamp(_launchAnglesDegrees.x, -MaxLaunchAngleDegrees, MaxLaunchAngleDegrees);
                        _launchAnglesDegrees.y = Mathf.Clamp(_launchAnglesDegrees.y, -MaxLaunchAngleDegrees, MaxLaunchAngleDegrees);
                        ResetPitch();
                    }
                }
            }

            if (IsBusy)
            {
                _animationElapsed += Time.deltaTime * _ball.PlaybackSpeed;
                if (_state == State.Windup && !_releaseQueued && _animationElapsed > _releaseEventSeconds + ReleaseEventGraceSeconds)
                {
                    // イベントの欠落を別の時刻で補わず、クリップの設定を確認できるようにする。
                    ResetPitch();
                    _statusText.text = "投球クリップのReleasedBallイベントが届きませんでした。";
                    Debug.LogError("[投球テスト] ReleasedBallイベントが届きませんでした。", this);
                }
                else if (_state == State.Flight && !_ball.IsPlaying) _state = State.Recovery;
                if (_state == State.Recovery && _animationElapsed >= _pitchAnimationSeconds) FinishPitch();
            }

            Vector2 mouse = Input.mousePosition;
            if (!IsBusy && !switched && (mouse != _lastMousePosition || Input.GetMouseButtonDown(0)))
            {
                if (TryPointerTarget(mouse, out var point))
                {
                    _hover = point;
                    if (Input.GetMouseButtonDown(0)) TryPitch(point);
                }
            }
            _lastMousePosition = mouse;
        }

        private bool TryPointerTarget(Vector2 mouse, out Vector3 point)
        {
            _uiHits.Clear();
            if (EventSystem.current != null)
                EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = mouse }, _uiHits);
            if (_uiHits.Count > 0)
            {
                // UIの球種選択やボタン操作を、ワールドへの投球クリックとして扱わない。
                if (_uiHits[0].gameObject.transform != _targetMap) { point = default; return false; }
                return PitchTargetProjection.TryFromPanel(_targetMap, mouse, _strikeZone.bounds, out point);
            }
            return PitchTargetProjection.TryFromScreen(ActiveCamera, mouse, _strikeZone.bounds, out point);
        }

        public void ChangeCamera(int direction)
        {
            _viewIndex = (_viewIndex + direction % _views.Length + _views.Length) % _views.Length;
            for (int i = 0; i < _views.Length; i++)
            {
                bool active = i == _viewIndex;
                _views[i].Camera.gameObject.SetActive(active);
                _views[i].Camera.tag = active ? "MainCamera" : "Untagged";
            }
            _cameraText.text = $"{_viewIndex + 1}/{_views.Length}  {_views[_viewIndex].Name}";
            var visualScaler = _ball.GetComponent<BallDistanceVisualScaler>();
            if (visualScaler != null)
            {
                visualScaler.SetTargetCamera(_views[_viewIndex].Camera);
                visualScaler.SetUnscaledRegion(_strikeZone.bounds);
            }
            // カメラだけを切り替えたフレームでは、マウス位置から通過点を作り直さない。
            _lastMousePosition = Input.mousePosition;
        }

        public bool TryPitch(Vector3 point)
        {
            if (IsBusy) return false;
            if (_pitchTypes == null || _pitchDropdown.value >= _pitchTypes.Length || _pitchTypes[_pitchDropdown.value] == null)
            {
                _statusText.text = "球種アセットを設定してください。";
                return false;
            }
            EnsureComparisonReferences();
            _queuedPitch = _pitchTypes[_pitchDropdown.value].Data;
            _queuedLaunchMode = _launchMode;
            _queuedLaunchDirection = Quaternion.Euler(-_launchAnglesDegrees.y, _launchAnglesDegrees.x, 0f) * Vector3.forward;
            _target = _hover = PitchTargetProjection.ClampToZone(point, _strikeZone.bounds);
            _hasTarget = true;
            _hasCross = false;
            _crossMarker.gameObject.SetActive(false);
            _trajectoryLine.positionCount = 0;
            _referenceTrajectoryLine.positionCount = 0;
            if (_comparisonText != null) _comparisonText.text = "青線: 同じ初速の無回転球との比較";
            RestorePitcher();
            _ball.ResetAt(_releasePoint);
            _pitcherAnimator.Play("Base Layer.Pitch", 0, 0);
            _animationElapsed = 0;
            _releaseQueued = false;
            _state = State.Windup;
            SetPitchControls(false);
            _statusText.text = $"{_queuedPitch.Name}  {_queuedPitch.Speed:F1} km/h\n{ModeDescription()}\n投球動作中";
            return true;
        }

        public void RepeatPitch()
        {
            if (_hasTarget || _launchMode == PitchLaunchMode.Direction) TryPitch(_target);
        }

        public void QueueRelease()
        {
            if (_state == State.Windup) _releaseQueued = true;
        }

        private void LateUpdate()
        {
            // アニメーションが手の位置を更新し終えてから、その位置をリリース点に使う。
            if (_releaseQueued && _state == State.Windup) ReleasePitch();
            else if (_state == State.Windup || _state == State.Ready) _ball.ResetAt(_releasePoint);
            UpdateMarkers();
        }

        private void ReleasePitch()
        {
            _releaseQueued = false;
            try
            {
                var comparison = PitchTestTrajectoryComparison.Calculate(new PitchRequest {
                    BallData = _queuedPitch, ReleasePoint = _releasePoint.position, PassPoint = _target,
                    StopZ = _stopZ, Settings = _settings, LaunchMode = _queuedLaunchMode,
                    LaunchDirection = _queuedLaunchDirection });
                var trajectory = comparison.Trajectory;
                bool reached = BallTrajectoryPredictor.TryGetCrossPointAtZ(trajectory, _target.z, out Vector3 cross);
                if (!reached && _queuedLaunchMode == PitchLaunchMode.TargetPoint)
                    throw new InvalidOperationException("軌道が指定したZ平面へ到達しませんでした。");
                float error = BaseballWorldScale.ToMeters(Vector2.Distance(new Vector2(cross.x, cross.y), new Vector2(_target.x, _target.y)));
                _crossMarker.position = cross;
                _hasCross = reached;
                _ball.Play(trajectory, _settings.DeltaTime, comparison.Spin);
                _trajectoryLine.positionCount = trajectory.Count;
                _trajectoryLine.SetPositions(trajectory.ToArray());
                SetTrajectoryVisible(_trajectoryToggle.isOn);
                _referenceTrajectoryLine.positionCount = comparison.ReferenceTrajectory.Count;
                _referenceTrajectoryLine.SetPositions(comparison.ReferenceTrajectory.ToArray());
                SetComparisonVisible(_comparisonToggle == null || _comparisonToggle.isOn);
                if (_comparisonText != null && reached && BallTrajectoryPredictor.TryGetCrossPointAtZ(comparison.ReferenceTrajectory, _target.z, out var referenceCross))
                {
                    Vector3 difference = BaseballWorldScale.ToMeters(cross - referenceCross);
                    string vertical = difference.y < 0 ? "下" : "上";
                    string horizontal = difference.x < 0 ? "左" : "右";
                    _comparisonText.text = $"青線: 無回転球（同じ初速）\n通過時は無回転球より {vertical} {Mathf.Abs(difference.y) * 100:F1} cm / {horizontal} {Mathf.Abs(difference.x) * 100:F1} cm";
                }
                else if (_comparisonText != null) _comparisonText.text = "目標Z平面で比較できませんでした。軌道全体は表示します。";
                _state = State.Flight;
                Vector3 crossMeters = BaseballWorldScale.ToMeters(cross);
                float transverse = comparison.Spin.GetTransverseRatio(comparison.InitialVelocityMps);
                string differenceLabel = _queuedLaunchMode == PitchLaunchMode.TargetPoint ? "XY誤差" : "基準点との差";
                string report = $"{_queuedPitch.Name}  {comparison.InitialVelocityMps.magnitude * BallPhysicsConstants.MPS_TO_KPH:F1} km/h\n{ModeDescription()} / 直交成分 {transverse:P1}\n通過(m): X={crossMeters.x:F3}  Y={crossMeters.y:F3}  Z={crossMeters.z:F3}\n{differenceLabel}: {error * 1000:F1} mm";
                if (!reached)
                    report = $"{_queuedPitch.Name}  {comparison.InitialVelocityMps.magnitude * BallPhysicsConstants.MPS_TO_KPH:F1} km/h\n{ModeDescription()}\n計算時間内に目標Z平面へ到達しませんでした。";
                _statusText.text = report;
                Debug.Log("[投球テスト] " + report.Replace('\n', ' '), this);
            }
            catch (Exception exception)
            {
                ResetPitch();
                _statusText.text = "投球を計算できませんでした。\n" + exception.Message;
                Debug.LogException(exception, this);
            }
        }

        public void ResetPitch()
        {
            EnsureComparisonReferences();
            _state = State.Ready;
            _releaseQueued = false;
            _hasCross = false;
            _hasTarget = false;
            _trajectoryLine.positionCount = 0;
            _referenceTrajectoryLine.positionCount = 0;
            if (_comparisonText != null) _comparisonText.text = "黄線: 選択した球 / 青線: 同じ初速の無回転球\n両方とも重力と空気抵抗を計算します。";
            RestorePitcher();
            _ball.ResetAt(_releasePoint);
            SetPitchControls(true);
            _statusText.text = ModeDescription() + (_launchMode == PitchLaunchMode.Direction
                ? "\nクリックまたはSpaceで投球。黄色の点は比較用の基準です。\nI/K: 仰角、J/L: 左右角を0.1度ずつ変更。Tでコース指定。"
                : "\n通過点をクリックすると投球します。\n真横・真上の視点では右の2Dパネルを使えます。Tで方向指定。");
        }

        private void RestorePitcher()
        {
            _pitcherRoot.SetPositionAndRotation(_rootPosition, _rootRotation);
            _pitcherAnimator.Rebind();
            _pitcherAnimator.Play("Base Layer.Idle", 0, 0);
            _pitcherAnimator.Update(0);
            _pitcherAnimator.speed = _ball.PlaybackSpeed;
        }

        private void FinishPitch()
        {
            _state = State.Ready;
            RestorePitcher();
            SetPitchControls(true);
        }

        private void SetPitchControls(bool ready)
        {
            _pitchDropdown.interactable = ready;
            _repeatButton.interactable = ready && (_hasTarget || _launchMode == PitchLaunchMode.Direction);
        }

        public void SetPlaybackSpeed(float speed)
        {
            _ball.PlaybackSpeed = Mathf.Clamp(speed, .1f, 1f);
            _pitcherAnimator.speed = _ball.PlaybackSpeed;
        }

        public void SetTrajectoryVisible(bool visible) => _trajectoryLine.enabled = visible;

        public void SetComparisonVisible(bool visible)
        {
            EnsureComparisonReferences();
            _referenceTrajectoryLine.enabled = visible;
        }

        public void ShowCloseView()
        {
            if (_closeSideViewIndex < _views.Length) ChangeCamera(_closeSideViewIndex - _viewIndex);
        }

        private void EnsureComparisonReferences()
        {
            if (_referenceTrajectoryLine == null)
            {
                var obj = new GameObject("No Spin Reference Trajectory");
                obj.transform.SetParent(transform, false);
                _referenceTrajectoryLine = obj.AddComponent<LineRenderer>();
                _referenceTrajectoryLine.sharedMaterial = _trajectoryLine.sharedMaterial;
                _referenceTrajectoryLine.useWorldSpace = true;
                _referenceTrajectoryLine.positionCount = 0;
                _referenceTrajectoryLine.startColor = _referenceTrajectoryLine.endColor = new Color(.25f, .65f, 1);
                _referenceTrajectoryLine.startWidth = _referenceTrajectoryLine.endWidth = .012f;
                _referenceTrajectoryLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _referenceTrajectoryLine.receiveShadows = false;
            }
        }

        private string ModeDescription() => _launchMode == PitchLaunchMode.Direction
            ? $"方向指定: 左右 {_launchAnglesDegrees.x:F1}° / 仰角 {_launchAnglesDegrees.y:F1}°"
            : "コース指定: 球速と回転軸を保ち、投げる方向を探索";

        private void ConfigurePhysicsLabels()
        {
            var repeatLabel = _repeatButton.GetComponentInChildren<TMP_Text>();
            if (repeatLabel != null) repeatLabel.text = "再投球 [Space]";
            if (_comparisonToggle != null)
            {
                var label = _comparisonToggle.GetComponentInChildren<TMP_Text>();
                if (label != null) label.text = "無回転との比較を表示";
            }
            foreach (var label in _pitchDropdown.transform.parent.GetComponentsInChildren<TMP_Text>(true))
                if (label.text.StartsWith("マウス移動"))
                    label.text = "クリック: 投球 / Space: 再投球\nT: 方向指定・コース指定\nI/K: 上下、J/L: 左右\n矢印: 視点 / F: 接近・横";
        }

        private void UpdateMarkers()
        {
            _hoverMarker.position = IsBusy ? _target : _hover;
            _targetMarker.position = _target;
            _targetMarker.gameObject.SetActive(_hasTarget);
            _crossMarker.gameObject.SetActive(_hasCross);
            SetMapMarker(_mapHoverMarker, IsBusy ? _target : _hover, true);
            SetMapMarker(_mapTargetMarker, _target, _hasTarget);
            SetMapMarker(_mapCrossMarker, _crossMarker.position, _hasCross);
        }

        private void SetMapMarker(RectTransform marker, Vector3 point, bool visible)
        {
            marker.gameObject.SetActive(visible);
            marker.anchorMin = marker.anchorMax = PitchTargetProjection.ToNormalized(point, _strikeZone.bounds);
            marker.anchoredPosition = Vector2.zero;
        }
    }
}
