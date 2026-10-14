using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Homerunderby
{
    [DefaultExecutionOrder(-100)]
    public sealed class PitchTestReleaseTimingPreview : MonoBehaviour
    {
        private enum ReleaseTiming { Current, OneFrameEarly, TwoFramesEarly }

        [SerializeField] private PitchTestSceneController _controller;
        [SerializeField] private Animator _pitcherAnimator;
        [SerializeField] private AnimationClip _pitchClip;
        [SerializeField] private Transform _releasePoint;
        [SerializeField] private LineRenderer _trajectoryLine;
        [SerializeField] private TMP_Dropdown _pitchDropdown;
        [SerializeField] private TMP_Text _textTemplate;
        [SerializeField] private float _originalEventTimeSeconds = 3f;
        [SerializeField] private ReleaseTiming _timing;

        private static readonly int PitchState = Animator.StringToHash("Base Layer.Pitch");
        private const float ClipFrameSeconds = 1f / 30f;

        private TMP_Text _timingText;
        private LineRenderer _previousLine;
        private Vector3[] _previousPoints;
        private Vector3 _previousTarget;
        private ReleaseTiming _previousTiming;
        private ReleaseTiming _activeTiming;
        private int _previousPitchIndex = -1;
        private bool _inPitchState;
        private bool _releasedThisPitch;
        private bool _savedThisPitch;
        private bool _showPrevious;
        private float _lastReleaseZ = float.NaN;
        private float _lastReleaseTime = float.NaN;

        private void Awake()
        {
            if (_controller == null || _pitcherAnimator == null || _pitchClip == null ||
                _releasePoint == null || _trajectoryLine == null || _pitchDropdown == null || _textTemplate == null)
            {
                Debug.LogError("PitchTestReleaseTimingPreview の参照を確認してください。", this);
                enabled = false;
                return;
            }

            CreateTimingLabel();
            CreatePreviousLine();
            UpdateLabel();
        }

        private void Update()
        {
            if (_controller.IsBusy) return;

            ReleaseTiming selected = _timing;
            if (Input.GetKeyDown(KeyCode.Alpha1)) selected = ReleaseTiming.Current;
            if (Input.GetKeyDown(KeyCode.Alpha2)) selected = ReleaseTiming.OneFrameEarly;
            if (Input.GetKeyDown(KeyCode.Alpha3)) selected = ReleaseTiming.TwoFramesEarly;
            if (selected == _timing) return;

            _timing = selected;
            RefreshPreviousLine();
            UpdateLabel();
        }

        private void LateUpdate()
        {
            AnimatorStateInfo state = _pitcherAnimator.GetCurrentAnimatorStateInfo(0);
            bool isPitch = _controller.IsBusy && state.fullPathHash == PitchState;
            if (!isPitch)
            {
                _inPitchState = false;
                _releasedThisPitch = false;
                _savedThisPitch = false;
            }
            else
            {
                if (!_inPitchState)
                {
                    _inPitchState = true;
                    _activeTiming = _timing;
                    _releasedThisPitch = false;
                    _savedThisPitch = false;
                }

                float clipSeconds = state.normalizedTime * _pitchClip.length;
                float selectedTime = _originalEventTimeSeconds - (int)_activeTiming * ClipFrameSeconds;
                if (!_releasedThisPitch && clipSeconds + 0.0001f >= selectedTime)
                {
                    // アニメーションが手の位置を更新した後に、テスト用のリリース時刻を適用する。
                    if (_activeTiming != ReleaseTiming.Current)
                        _controller.QueueRelease();

                    _lastReleaseZ = BaseballWorldScale.ToMeters(_releasePoint.position.z);
                    _lastReleaseTime = clipSeconds;
                    _releasedThisPitch = true;
                    UpdateLabel();
                    Debug.Log($"[投球テスト] リリース時刻={clipSeconds:F3}s、位置Z={_lastReleaseZ:F3}m", this);
                }

                if (!_savedThisPitch && _trajectoryLine.positionCount > 1)
                {
                    _previousPoints = new Vector3[_trajectoryLine.positionCount];
                    _trajectoryLine.GetPositions(_previousPoints);
                    _previousTarget = _controller.TargetPoint;
                    _previousPitchIndex = _pitchDropdown.value;
                    _previousTiming = _activeTiming;
                    _savedThisPitch = true;
                }
            }

            if (_showPrevious && (_pitchDropdown.value != _previousPitchIndex ||
                                  (_controller.TargetPoint - _previousTarget).sqrMagnitude > 0.000001f))
                _showPrevious = false;
            _previousLine.enabled = _showPrevious && _trajectoryLine.enabled;
        }

        private void CreateTimingLabel()
        {
            Canvas canvas = _textTemplate.GetComponentInParent<Canvas>();
            if (canvas == null) throw new MissingReferenceException("投球テストのCanvasが見つかりません。");

            var panel = new GameObject("Release Timing Preview", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canvas.transform, false);
            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 1f);
            panelRect.pivot = new Vector2(0.5f, 1f);
            panelRect.anchoredPosition = new Vector2(0f, -10f);
            panelRect.sizeDelta = new Vector2(640f, 86f);
            var background = panel.GetComponent<Image>();
            background.color = new Color(0.05f, 0.08f, 0.12f, 0.78f);
            background.raycastTarget = false;

            _timingText = Instantiate(_textTemplate, panel.transform);
            _timingText.name = "Release Timing Text";
            _timingText.raycastTarget = false;
            _timingText.alignment = TextAlignmentOptions.Center;
            _timingText.fontSize = 19f;
            _timingText.enableAutoSizing = false;
            var textRect = (RectTransform)_timingText.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(12f, 4f);
            textRect.offsetMax = new Vector2(-12f, -4f);
        }

        private void CreatePreviousLine()
        {
            var lineObject = new GameObject("Previous Release Trajectory");
            lineObject.transform.SetParent(transform, false);
            _previousLine = lineObject.AddComponent<LineRenderer>();
            _previousLine.sharedMaterial = _trajectoryLine.sharedMaterial;
            _previousLine.useWorldSpace = true;
            _previousLine.startWidth = _previousLine.endWidth = Mathf.Max(_trajectoryLine.startWidth, 0.015f);
            _previousLine.startColor = _previousLine.endColor = new Color(0.95f, 0.35f, 1f);
            _previousLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _previousLine.receiveShadows = false;
            _previousLine.enabled = false;
        }

        private void RefreshPreviousLine()
        {
            _showPrevious = _previousPoints != null && _previousPoints.Length > 1 &&
                            _previousTiming != _timing && _previousPitchIndex == _pitchDropdown.value &&
                            (_controller.TargetPoint - _previousTarget).sqrMagnitude <= 0.000001f;
            if (!_showPrevious) return;

            _previousLine.positionCount = _previousPoints.Length;
            _previousLine.SetPositions(_previousPoints);
        }

        private void UpdateLabel()
        {
            string selected = _timing == ReleaseTiming.Current ? "現行 3.000秒" :
                _timing == ReleaseTiming.OneFrameEarly ? "1コマ早い 2.967秒" : "2コマ早い 2.933秒";
            string lastPosition = float.IsNaN(_lastReleaseZ) ? "" :
                $"  |  前回 {_lastReleaseTime:F3}秒 / Z {_lastReleaseZ:F2}m";
            _timingText.text = $"リリース: [1] 現行  [2] 1コマ早い  [3] 2コマ早い\n" +
                               $"選択: {selected}{lastPosition}\n" +
                               "Space: 同じコースへ再投球  |  紫: 直前の軌道";
        }
    }
}
