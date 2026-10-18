using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent, DefaultExecutionOrder(1200)]
[RequireComponent(typeof(BattingSystem), typeof(BatSwingAimIK))]
public sealed class BattingBallTimingIndicator : MonoBehaviour
{
    private const float BallRingInnerRatio = 0.55f;
    private const float TimingRingDiameterRatio = 0.45f;
    private const float TimingRingFitScale = BallRingInnerRatio / TimingRingDiameterRatio;
    private const float GoodImpactEfficiency = 0.4f;
    private const float PerfectImpactEfficiency = 0.8f;
    private const float GradeArcStepDegrees = 18f;
    private const float GradeArcCenterDegrees = 45f;
    private const float GradeArcOffsetPixels = 30f;
    private const int MaxGradeLetters = 7;

    [Tooltip("ボール位置を示す輪の直径（画面上のピクセル）。")]
    [SerializeField, Min(8f)] private float _ballDiameter = 32f;
    [Tooltip("打撃面をボールが通過する予測位置の目印の直径（画面上のピクセル）。")]
    [SerializeField, Min(8f)] private float _crossPointDiameter = 48f;
    [Tooltip("投球開始時の接近リングの大きさ。打撃判定には影響しません。")]
    [SerializeField, Min(1f)] private float _startingScale = 12f;
    [Tooltip("ジャスト入力の前後の許容時間（秒）。ゲージ表示と入力評価で共通して使います。")]
    [SerializeField, Min(0f)] private float _perfectWindowSeconds = 0.02f;
    [Tooltip("入力時刻を過ぎた後に赤い輪を表示する時間。")]
    [SerializeField, Min(0.01f)] private float _lateDisplaySeconds = 0.2f;
    [SerializeField] private Color _ballColor = new Color(0.1f, 0.8f, 1f, 0.8f);
    [SerializeField] private Color _crossPointColor = new Color(1f, 1f, 1f, 0.8f);
    [SerializeField] private Color _earlyColor = new Color(1f, 0.8f, 0.2f, 0.8f);
    [SerializeField] private Color _justColor = new Color(0.2f, 1f, 0.45f, 0.9f);
    [SerializeField] private Color _lateColor = new Color(1f, 0.3f, 0.3f, 0.8f);

    private BattingSystem _batting;
    private BatSwingAimIK _ik;
    private PitchBallMove _ball;
    private Canvas _canvas;
    private RectTransform _canvasRect;
    private RectTransform _indicatorRect;
    private RectTransform _crossPointRect;
    private RectTransform _timingRect;
    private Image _timingRing;
    private RectTransform _gradeRect;
    private readonly TextMeshProUGUI[] _gradeLetters = new TextMeshProUGUI[MaxGradeLetters];
    private Sprite _ringSprite;
    private Sprite _timingSprite;
    private Sprite _crossPointSprite;
    private Coroutine _gradeHideRoutine;
    private bool _frozen;
    private bool _keepFrozenUntilHitStopEnds;
    private bool _inputWasPerfect;
    private float _initialTimeUntilInput;
    private Vector3 _justPosition;
    private bool _hasJustPosition;
    private bool _showCrossPoint;
    private Vector3 _predictionAtSwing;
    private bool _hasPredictionAtSwing;
    private float _lastCueLeadSeconds;
    private float PerfectInputToleranceSeconds => Mathf.Max(0f, _perfectWindowSeconds);

    private bool TryGetTimeUntilPerfectInput(out float remainingSeconds, out float contactDelaySeconds)
    {
        remainingSeconds = 0f;
        contactDelaySeconds = 0f;
        if (_ball == null || !_ball.IsMoving || _frozen || !_hasJustPosition ||
            !_ik.TryGetContactDelay(out contactDelaySeconds) ||
            !_ball.TryGetTimeUntilZ(_justPosition.z, out float timeUntilPlane))
            return false;

        remainingSeconds = timeUntilPlane - contactDelaySeconds;
        return true;
    }

    private void Awake()
    {
        _batting = GetComponent<BattingSystem>();
        _ik = GetComponent<BatSwingAimIK>();
        BattingCursor cursor = _ik.Cursor;
        if (cursor == null || cursor.CursorCanvas == null || cursor.CursorRect == null)
        {
            Debug.LogError("[BattingBallTimingIndicator] カーソルのCanvasが見つかりません。", this);
            enabled = false;
            return;
        }

        _canvas = cursor.CursorCanvas;
        _canvasRect = _canvas.transform as RectTransform;
        CreateUI(cursor.CursorRect);
    }

    private void OnEnable()
    {
        if (_indicatorRect == null) return;
        _batting.PitchReleased += OnPitchReleased;
        _batting.SwingStarted += OnSwingStarted;
        _batting.ContactCaptured += OnContactCaptured;
        PitchBallMove currentPitch = _batting.CurrentPitch;
        if (_batting.CanSwing && currentPitch != null && currentPitch.IsMoving)
            OnPitchReleased(currentPitch);
    }

    private void OnDisable()
    {
        if (_batting != null)
        {
            _batting.PitchReleased -= OnPitchReleased;
            _batting.SwingStarted -= OnSwingStarted;
            _batting.ContactCaptured -= OnContactCaptured;
        }
        if (_indicatorRect != null) _indicatorRect.gameObject.SetActive(false);
        if (_crossPointRect != null) _crossPointRect.gameObject.SetActive(false);
        _showCrossPoint = false;
        HideGrade();
    }

    private void OnDestroy()
    {
        if (_indicatorRect != null) Destroy(_indicatorRect.gameObject);
        if (_crossPointRect != null) Destroy(_crossPointRect.gameObject);
        if (_ringSprite != null)
        {
            Destroy(_ringSprite.texture);
            Destroy(_ringSprite);
        }
        if (_timingSprite != null)
        {
            Destroy(_timingSprite.texture);
            Destroy(_timingSprite);
        }
        if (_crossPointSprite != null)
        {
            Destroy(_crossPointSprite.texture);
            Destroy(_crossPointSprite);
        }
    }

    private void OnPitchReleased(PitchBallMove ball)
    {
        _ball = ball;
        _frozen = false;
        _keepFrozenUntilHitStopEnds = false;
        _inputWasPerfect = false;
        HideGrade();
        _initialTimeUntilInput = 0f;
        _hasJustPosition = ball != null && _ik.Cursor != null &&
            BallTrajectoryPredictor.TryGetCrossPointAtZ(ball.Trajectory,
                _ik.Cursor.CurrentPos.z, out _justPosition);
        _showCrossPoint = _hasJustPosition;
        UpdateCrossPoint();
        _hasPredictionAtSwing = false;
        _lastCueLeadSeconds = 0f;
        _timingRing.enabled = false;
        _indicatorRect.gameObject.SetActive(_hasJustPosition);
    }

    private void OnSwingStarted()
    {
        if (_ball == null) return;
        bool hasTiming = TryGetTimeUntilPerfectInput(out float remainingSeconds, out float contactDelay);
        _inputWasPerfect = hasTiming && Mathf.Abs(remainingSeconds) <= PerfectInputToleranceSeconds;
        _hasPredictionAtSwing = hasTiming &&
            _ball.TryGetPredictedPosition(contactDelay, out _predictionAtSwing);
        if (_batting.Parameters == null || _batting.Parameters.EnableDebugLogs)
        {
            Debug.Log($"[Batting/TimingUI] inputToJust={(hasTiming ? remainingSeconds.ToString("F4") : "unavailable")}s " +
                $"cueLead={_lastCueLeadSeconds:F4}s " +
                $"predictedContact={(_hasPredictionAtSwing ? _predictionAtSwing.ToString("F4") : "unavailable")} " +
                $"justPoint={(_hasJustPosition ? _justPosition.ToString("F4") : "unavailable")}", this);
        }
        _frozen = true;
        if (_indicatorRect.gameObject.activeSelf && _inputWasPerfect)
        {
            _timingRing.enabled = true;
            _timingRing.sprite = _ringSprite;
            _timingRing.color = _justColor;
            _timingRect.localScale = Vector3.one;
        }
        else if (_indicatorRect.gameObject.activeSelf && hasTiming && remainingSeconds < 0f)
        {
            _timingRing.enabled = true;
            _timingRing.sprite = _ringSprite;
            _timingRing.color = _lateColor;
            _timingRect.localScale = Vector3.one * (TimingRingFitScale + 0.2f);
        }
    }

    private void OnContactCaptured()
    {
        _showCrossPoint = false;
        _crossPointRect.gameObject.SetActive(false);
        if (_hasPredictionAtSwing && (_batting.Parameters == null || _batting.Parameters.EnableDebugLogs) &&
            _batting.TryGetContact(_ball, out _, out Vector3 contactPosition))
        {
            Vector3 predictionErrorMeters = BaseballWorldScale.ToMeters(contactPosition - _predictionAtSwing);
            Debug.Log($"[Batting/TimingUI] predictionError={predictionErrorMeters.ToString("F4")}m", this);
        }
        if (!_batting.TryGetContact(_ball, out Vector3 cursorPosition, out Vector3 ballPosition) ||
            _batting.Parameters == null)
        {
            HideIndicator();
            HideGrade();
            return;
        }

        BattingParameters parameters = _batting.Parameters;
        float impactDistance = BaseballWorldScale.ToMeters(Vector3.Distance(cursorPosition, ballPosition));
        if (impactDistance > parameters.MaxImpactDistance)
        {
            // 空振りは当たり方の評価に含めない。
            HideIndicator();
            HideGrade();
            return;
        }

        if (!_indicatorRect.gameObject.activeSelf)
        {
            Camera camera = Camera.main;
            if (camera != null)
            {
                Vector3 screenPoint = camera.WorldToScreenPoint(ballPosition);
                if (screenPoint.z > 0f && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        _canvasRect, screenPoint, null, out Vector2 localPoint))
                {
                    _indicatorRect.anchoredPosition = localPoint;
                    _indicatorRect.gameObject.SetActive(true);
                }
            }
        }

        float efficiency = BattingPhysics.CalculateImpactEfficiency(impactDistance,
            parameters.SweetSpotRadius, parameters.MaxImpactDistance,
            parameters.ImpactEfficiencyCurve, parameters.MinImpactEfficiency);
        if (efficiency < GoodImpactEfficiency)
            ShowGrade("BAD", _lateColor);
        else if (_inputWasPerfect && efficiency >= PerfectImpactEfficiency)
            ShowGrade("PERFECT", _justColor);
        else
            ShowGrade("GOOD", _earlyColor);

        _keepFrozenUntilHitStopEnds = _batting.IsHitStopped;
        if (!_keepFrozenUntilHitStopEnds)
            _gradeHideRoutine = StartCoroutine(HideGradeAfterDelay());
    }

    private void LateUpdate()
    {
        if (_indicatorRect != null && _canvas != null)
            _indicatorRect.localScale = Vector3.one / Mathf.Max(0.01f, _canvas.scaleFactor);
        if (_crossPointRect != null && _canvas != null)
            _crossPointRect.localScale = Vector3.one / Mathf.Max(0.01f, _canvas.scaleFactor);
        UpdateCrossPoint();

        if (_frozen)
        {
            if (_keepFrozenUntilHitStopEnds && !_batting.IsHitStopped)
            {
                HideIndicator();
                HideGrade();
            }
            return;
        }

        if (_ball == null || !_ball.IsMoving)
        {
            if (_indicatorRect != null) _indicatorRect.gameObject.SetActive(false);
            return;
        }

        UpdateIndicator();
    }

    private void HideIndicator()
    {
        _ball = null;
        _keepFrozenUntilHitStopEnds = false;
        if (_indicatorRect != null) _indicatorRect.gameObject.SetActive(false);
        _showCrossPoint = false;
        if (_crossPointRect != null) _crossPointRect.gameObject.SetActive(false);
    }

    private void UpdateCrossPoint()
    {
        if (_crossPointRect == null) return;
        Camera camera = Camera.main;
        if (!_showCrossPoint || _ball == null || !_ball.IsMoving || camera == null)
        {
            _crossPointRect.gameObject.SetActive(false);
            return;
        }

        Vector3 screenPoint = camera.WorldToScreenPoint(_justPosition);
        if (screenPoint.z <= 0f || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRect, screenPoint, null, out Vector2 localPoint))
        {
            _crossPointRect.gameObject.SetActive(false);
            return;
        }

        _crossPointRect.anchoredPosition = localPoint;
        _crossPointRect.gameObject.SetActive(true);
    }

    private void ShowGrade(string label, Color color)
    {
        if (_gradeRect == null) return;
        HideGrade();
        float radius = _ballDiameter * 0.5f + GradeArcOffsetPixels;
        float firstAngle = GradeArcCenterDegrees + (label.Length - 1) * GradeArcStepDegrees * 0.5f;
        for (int i = 0; i < _gradeLetters.Length; i++)
        {
            TextMeshProUGUI letter = _gradeLetters[i];
            bool visible = i < label.Length;
            letter.gameObject.SetActive(visible);
            if (!visible) continue;
            float angle = firstAngle - i * GradeArcStepDegrees;
            float radians = angle * Mathf.Deg2Rad;
            letter.text = label[i].ToString();
            letter.color = color;
            letter.rectTransform.anchoredPosition = new Vector2(
                Mathf.Cos(radians) * radius, Mathf.Sin(radians) * radius);
            letter.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle - 90f);
        }
        _gradeRect.gameObject.SetActive(true);
    }

    private void HideGrade()
    {
        if (_gradeHideRoutine != null) StopCoroutine(_gradeHideRoutine);
        _gradeHideRoutine = null;
        if (_gradeRect != null) _gradeRect.gameObject.SetActive(false);
    }

    private IEnumerator HideGradeAfterDelay()
    {
        yield return new WaitForSecondsRealtime(0.75f);
        _gradeHideRoutine = null;
        HideIndicator();
        HideGrade();
    }

    private void UpdateIndicator()
    {
        Camera camera = Camera.main;
        if (camera == null || _ik.Cursor == null || !_hasJustPosition) return;

        // 次の入力フレームから接触姿勢までに進むボールを、カーソルと同じCanvasに表示する。
        if (!TryGetTimeUntilPerfectInput(out float remainingSeconds, out float contactDelay))
        {
            _indicatorRect.gameObject.SetActive(false);
            return;
        }

        // 次の入力フレームの残り時間を表示し、入力側も同じ残り時間で判定する。
        _lastCueLeadSeconds = Time.deltaTime;
        float displayDelay = contactDelay + _lastCueLeadSeconds;
        float timeUntilJustInput = remainingSeconds - _lastCueLeadSeconds;
        float toleranceSeconds = PerfectInputToleranceSeconds;
        bool nextInputIsPerfect = Mathf.Abs(timeUntilJustInput) <= toleranceSeconds;
        Vector3 contactBallPosition;
        if (timeUntilJustInput <= toleranceSeconds)
            contactBallPosition = _justPosition;
        else if (!_ball.TryGetPredictedPosition(displayDelay, out contactBallPosition))
        {
            _indicatorRect.gameObject.SetActive(false);
            return;
        }

        Vector3 screenPoint = camera.WorldToScreenPoint(contactBallPosition);
        if (screenPoint.z <= 0f || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRect, screenPoint, null, out Vector2 localPoint))
        {
            _indicatorRect.gameObject.SetActive(false);
            return;
        }

        _indicatorRect.gameObject.SetActive(true);
        _indicatorRect.anchoredPosition = localPoint;

        if (timeUntilJustInput < -_lateDisplaySeconds)
        {
            _timingRing.enabled = false;
            return;
        }

        _timingRing.enabled = true;
        float scale = TimingRingFitScale;
        float progress = 0f;
        if (timeUntilJustInput > toleranceSeconds)
        {
            if (_initialTimeUntilInput <= 0f) _initialTimeUntilInput = timeUntilJustInput;
            // 黄色い輪がボールの内側に入る時刻を、ジャスト入力の範囲に合わせる。
            float approach = (timeUntilJustInput - toleranceSeconds) /
                Mathf.Max(0.001f, _initialTimeUntilInput - toleranceSeconds);
            progress = Mathf.Sqrt(Mathf.Clamp01(approach));
            scale = Mathf.Lerp(TimingRingFitScale, _startingScale, progress);
        }

        if (nextInputIsPerfect)
        {
            scale = Mathf.Lerp(1f, TimingRingFitScale,
                Mathf.Clamp01(Mathf.Abs(timeUntilJustInput) / Mathf.Max(0.001f, toleranceSeconds)));
            if (_timingRing.sprite != _ringSprite) _timingRing.sprite = _ringSprite;
            _timingRing.color = _justColor;
        }
        else if (timeUntilJustInput > 0f)
        {
            if (_timingRing.sprite != _timingSprite) _timingRing.sprite = _timingSprite;
            Color earlyColor = _earlyColor;
            earlyColor.a *= Mathf.Lerp(1f, 0.55f, progress);
            _timingRing.color = earlyColor;
        }
        else
        {
            scale = TimingRingFitScale + 0.2f;
            if (_timingRing.sprite != _ringSprite) _timingRing.sprite = _ringSprite;
            _timingRing.color = _lateColor;
        }

        _timingRect.localScale = Vector3.one * scale;
    }

    private void CreateUI(RectTransform cursorRect)
    {
        _ringSprite = CreateCircleSprite(BallRingInnerRatio);
        _timingSprite = CreateCircleSprite(0.94f);
        _crossPointSprite = CreateCircleSprite(0.86f, 8);

        var crossPointObject = new GameObject("PredictedBallCrossPoint", typeof(RectTransform));
        _crossPointRect = crossPointObject.GetComponent<RectTransform>();
        _crossPointRect.SetParent(_canvasRect, false);
        _crossPointRect.anchorMin = _crossPointRect.anchorMax = new Vector2(0.5f, 0.5f);
        _crossPointRect.sizeDelta = Vector2.zero;
        _crossPointRect.SetSiblingIndex(cursorRect.GetSiblingIndex());
        CreateImage("CrossPointRing", _crossPointRect, _crossPointSprite,
            _crossPointDiameter, _crossPointColor);
        crossPointObject.SetActive(false);

        var root = new GameObject("BallTimingIndicator", typeof(RectTransform));
        _indicatorRect = root.GetComponent<RectTransform>();
        _indicatorRect.SetParent(_canvasRect, false);
        _indicatorRect.anchorMin = _indicatorRect.anchorMax = new Vector2(0.5f, 0.5f);
        _indicatorRect.sizeDelta = Vector2.zero;
        _indicatorRect.SetSiblingIndex(cursorRect.GetSiblingIndex());

        CreateImage("BallPosition", _indicatorRect, _ringSprite, _ballDiameter, _ballColor);
        _timingRing = CreateImage("SwingTiming", _indicatorRect, _timingSprite,
            _ballDiameter * TimingRingDiameterRatio, _earlyColor);
        _timingRect = _timingRing.rectTransform;
        CreateGrade();
        root.SetActive(false);
    }

    private void CreateGrade()
    {
        TMP_FontAsset gradeFont = TMP_Settings.defaultFontAsset;
        if (gradeFont == null)
        {
            foreach (TextMeshProUGUI text in _canvas.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (text.font == null) continue;
                gradeFont = text.font;
                break;
            }
        }
        var gradeObject = new GameObject("TimingGradeArc", typeof(RectTransform));
        _gradeRect = gradeObject.GetComponent<RectTransform>();
        _gradeRect.SetParent(_indicatorRect, false);
        _gradeRect.anchorMin = _gradeRect.anchorMax = new Vector2(0.5f, 0.5f);
        _gradeRect.sizeDelta = Vector2.zero;
        for (int i = 0; i < _gradeLetters.Length; i++)
        {
            var letterObject = new GameObject($"Letter{i}", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            letterObject.transform.SetParent(_gradeRect, false);
            TextMeshProUGUI letter = letterObject.GetComponent<TextMeshProUGUI>();
            if (gradeFont != null) letter.font = gradeFont;
            letter.fontSize = 20f;
            letter.fontStyle = FontStyles.Bold;
            letter.alignment = TextAlignmentOptions.Center;
            letter.outlineWidth = 0.2f;
            letter.outlineColor = Color.black;
            letter.raycastTarget = false;
            RectTransform rect = letter.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(24f, 28f);
            _gradeLetters[i] = letter;
        }
        gradeObject.SetActive(false);
    }

    private static Image CreateImage(string name, Transform parent, Sprite sprite,
        float diameter, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        image.rectTransform.sizeDelta = Vector2.one * diameter;
        return image;
    }

    private static Sprite CreateCircleSprite(float innerRadiusRatio, int dashCount = 0)
    {
        const int size = 128;
        float radius = size * 0.5f - 1f;
        float innerRadius = radius * innerRadiusRatio;
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - size * 0.5f;
                float dy = y + 0.5f - size * 0.5f;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01(radius - distance) *
                              (innerRadiusRatio <= 0f ? 1f : Mathf.Clamp01(distance - innerRadius));
                if (dashCount > 0)
                {
                    float dashPhase = Mathf.Repeat(Mathf.Atan2(dy, dx) / (2f * Mathf.PI) * dashCount, 1f);
                    if (dashPhase > 0.65f) alpha = 0f;
                }
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }
        }

        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.hideFlags = HideFlags.DontSave;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f), 100f);
        sprite.hideFlags = HideFlags.DontSave;
        return sprite;
    }
}
