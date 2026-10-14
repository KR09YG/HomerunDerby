using UnityEngine;

// ボール本体の座標やColliderには触れず、描画用の子だけを拡大する。
[DefaultExecutionOrder(10000)]
public sealed class BallDistanceVisualScaler : MonoBehaviour
{
    private const float StrikeZoneDepthMargin = 0.5f;
    [SerializeField] private Transform _visualTransform;
    [SerializeField] private Camera _targetCamera;
    [SerializeField, Min(0f), Tooltip("通常サイズに戻すカメラ奥行き。Unity単位。")]
    private float _nearDistance = 5f;
    [SerializeField, Min(0f), Tooltip("最大倍率になるカメラ奥行き。Unity単位。")]
    private float _farDistance = 22f;
    [SerializeField, Min(1f)] private float _maxScaleMultiplier = 1.6f;
    [SerializeField] private AnimationCurve _scaleCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private Vector3 _baseVisualScale;
    private Camera _camera;
    private bool _initialized, _warnedAboutCamera;
    private bool _scalingActive = true;
    private Bounds? _unscaledRegion;
    public float CurrentMultiplier { get; private set; } = 1f;

    private void Awake()
    {
        if (_visualTransform == null || _visualTransform == transform ||
            !_visualTransform.IsChildOf(transform) || _visualTransform.GetComponentInChildren<Collider>(true) != null ||
            _visualTransform.GetComponentInChildren<Rigidbody>(true) != null)
        {
            Debug.LogError("ボールの表示補正には、ColliderとRigidbodyを持たない描画専用の子を指定してください。", this);
            enabled = false;
            return;
        }
        _baseVisualScale = _visualTransform.localScale;
        _initialized = true;
        SetTargetCamera(_targetCamera);
    }

    public void SetTargetCamera(Camera camera)
    {
        _targetCamera = camera;
        // カメラ切り替え時だけ取得し直す。毎フレームMainCameraを検索しない。
        _camera = camera != null ? camera : Camera.main;
        if (_camera == null && !_warnedAboutCamera)
        {
            Debug.LogWarning("ボールの表示補正用カメラが見つからないため、通常サイズで表示します。", this);
            _warnedAboutCamera = true;
        }
        RestoreScale();
    }

    public void SetScalingActive(bool active)
    {
        _scalingActive = active;
        if (!active) RestoreScale();
    }

    public void SetUnscaledRegion(Bounds region) => _unscaledRegion = region;

    private void LateUpdate()
    {
        if (!_initialized) return;
        if (!_scalingActive || _camera == null || !_camera.isActiveAndEnabled || _camera.orthographic)
        {
            RestoreScale();
            return;
        }
        float depth = Vector3.Dot(transform.position - _camera.transform.position, _camera.transform.forward);
        CurrentMultiplier = EvaluateMultiplier(depth);
        _visualTransform.localScale = _baseVisualScale * CurrentMultiplier;
    }

    internal float EvaluateMultiplier(float depth)
    {
        if (!IsFinite(depth) || depth <= 0f || !IsFinite(_nearDistance) || !IsFinite(_farDistance) ||
            !IsFinite(_maxScaleMultiplier)) return 1f;
        float near = Mathf.Max(0f, _nearDistance);
        if (_unscaledRegion.HasValue && _camera != null)
        {
            // 真横・投手側の視点でも、ストライクゾーン全体を通常サイズで表示する。
            Bounds region = _unscaledRegion.Value;
            Vector3 forward = _camera.transform.forward;
            float extent = Vector3.Dot(new Vector3(Mathf.Abs(forward.x), Mathf.Abs(forward.y), Mathf.Abs(forward.z)), region.extents);
            near = Mathf.Max(near, Vector3.Dot(region.center - _camera.transform.position, forward) + extent + StrikeZoneDepthMargin);
        }
        float far = Mathf.Max(near, _farDistance);
        float maximum = Mathf.Max(1f, _maxScaleMultiplier);
        if (depth <= near) return 1f;
        if (depth >= far) return maximum;
        float t = Mathf.InverseLerp(near, far, depth);
        float weight = _scaleCurve != null && _scaleCurve.length > 0 ? _scaleCurve.Evaluate(t) : t;
        return IsFinite(weight) ? Mathf.Lerp(1f, maximum, Mathf.Clamp01(weight)) : 1f;
    }

    private void OnDisable() => RestoreScale();

    private void RestoreScale()
    {
        CurrentMultiplier = 1f;
        if (_initialized && _visualTransform != null) _visualTransform.localScale = _baseVisualScale;
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
