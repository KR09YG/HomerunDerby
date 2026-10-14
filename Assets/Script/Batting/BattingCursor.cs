using UnityEngine;
using UnityEngine.UI;

public class BattingCursor : MonoBehaviour
{
    [SerializeField] private GameObject _strikeZone;
    [SerializeField] private RectTransform _cursorRect;   
    [SerializeField] private Image _cursorImage;         
    [SerializeField] private Canvas _canvas;     
    [SerializeField] private OnSwingEvent _swingEvent;
    [SerializeField] private OnBattingInputEvent _battingInputEvent;
    [SerializeField] private OnAtBatResetEvent _atBatResetEvent;
    [SerializeField] private float _moveSpeed = 5f;
    [SerializeField] private float _moveRange = 0.5f;

    private Collider _strikeZoneCollider;
    private Vector3 _currentPos;         
    private bool _isInputActive = false;

    public Vector3 CurrentPos => _currentPos;
    public Canvas CursorCanvas => _canvas;
    public RectTransform CursorRect => _cursorRect;

    public void SetWorldPosition(Vector3 position)
    {
        _currentPos = position;
        UpdateCursorUI();
    }

    private void Awake()
    {
        if (_swingEvent != null) _swingEvent.RegisterListener(FinishInput);
        else Debug.LogWarning("_swingEvent is not assigned in BattingCursor.", this);
        if (_battingInputEvent != null) _battingInputEvent.RegisterListener(FinishInput);
        else Debug.LogWarning("_battingInputEvent is not assigned in BattingCursor.", this);
        if (_atBatResetEvent != null) _atBatResetEvent.RegisterListener(StartInput);
        else Debug.LogWarning("_atBatResetEvent is not assigned in BattingCursor.");
    }

    private void Start()
    {
        if (_strikeZone != null)
            _strikeZoneCollider = _strikeZone.GetComponent<Collider>();

        StartInput();
    }

    private void OnDestroy()
    {
        _swingEvent?.UnregisterListener(FinishInput);
        _battingInputEvent?.UnregisterListener(FinishInput);
        _atBatResetEvent?.UnregisterListener(StartInput);
    }

    private void Update()
    {
        if (!_isInputActive || _strikeZoneCollider == null) return;

        Camera camera = Camera.main;
        if (camera == null) return;

        // カメラを傾けても、マウスの指す位置を打撃判定のZ平面上に合わせる。
        Bounds bounds = _strikeZoneCollider.bounds;
        Ray ray = camera.ScreenPointToRay(Input.mousePosition);
        Plane battingPlane = new Plane(Vector3.forward, bounds.center);
        if (!battingPlane.Raycast(ray, out float distance)) return;

        Vector3 worldMousePosition = ray.GetPoint(distance);
        Vector3 clampedPosition = new Vector3(
            Mathf.Clamp(worldMousePosition.x, bounds.min.x - _moveRange, bounds.max.x + _moveRange),
            Mathf.Clamp(worldMousePosition.y, bounds.min.y - _moveRange, bounds.max.y + _moveRange),
            bounds.center.z);

        _currentPos = Vector3.Lerp(_currentPos, clampedPosition, Time.deltaTime * _moveSpeed);

        UpdateCursorUI();
    }

    private void UpdateCursorUI()
    {
        Vector2 screenPoint = Camera.main.WorldToScreenPoint(_currentPos);

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvas.transform as RectTransform,
                screenPoint,
                null,   // OverlayのCanvasではカメラを指定しない。
                out Vector2 localPoint))
        {
            _cursorRect.anchoredPosition = localPoint;
        }
    }

    private void StartInput()
    {
        Cursor.visible = false;
        _isInputActive = true;
        _cursorImage.enabled = true;

        if (_strikeZoneCollider != null)
        {
            _currentPos = _strikeZoneCollider.bounds.center;
            UpdateCursorUI();
        }
    }

    private void FinishInput()
    {
        Cursor.visible = true;
        _isInputActive = false;
        _cursorImage.enabled = false;
    }

    public void OnSetBatter()
    {
        _cursorImage.enabled = true;
        _currentPos = _strikeZoneCollider.bounds.center;
        UpdateCursorUI();
    }
}
