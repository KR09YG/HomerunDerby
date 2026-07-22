using UnityEngine;
using UnityEngine.UI;

public class BattingCursor : MonoBehaviour
{
    [SerializeField] private GameObject _strikeZone;
    [SerializeField] private RectTransform _cursorRect;   
    [SerializeField] private Image _cursorImage;         
    [SerializeField] private Canvas _canvas;     
    [SerializeField] private OnSwingEvent _swingEvent;
    [SerializeField] private OnAtBatResetEvent _atBatResetEvent;
    [SerializeField] private float _moveSpeed = 5f;
    [SerializeField] private float _moveRange = 0.5f;

    private Collider _strikeZoneCollider;
    private Vector3 _currentPos;         
    private bool _isInputActive = false;

    public Vector3 CurrentPos => _currentPos;

    private void Awake()
    {
        if (_swingEvent != null) _swingEvent.RegisterListener(FinishInput);
        else Debug.LogWarning("_swingEvent is not assigned in BattingCursor.");
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
        _atBatResetEvent?.UnregisterListener(StartInput);
    }

    private void Update()
    {
        if (!_isInputActive) return;

        // --- World座標の更新（従来通り）---
        Vector3 mousePosition = Input.mousePosition;
        mousePosition.z = Camera.main.WorldToScreenPoint(_strikeZoneCollider.bounds.center).z;
        Vector3 worldMousePosition = Camera.main.ScreenToWorldPoint(mousePosition);

        Vector3 clampedPosition = _currentPos;
        if (_strikeZoneCollider != null)
        {
            Bounds bounds = _strikeZoneCollider.bounds;
            clampedPosition.x = Mathf.Clamp(worldMousePosition.x, bounds.min.x - _moveRange, bounds.max.x + _moveRange);
            clampedPosition.y = Mathf.Clamp(worldMousePosition.y, bounds.min.y - _moveRange, bounds.max.y + _moveRange);
            clampedPosition.z = bounds.center.z;
        }

        _currentPos = Vector3.Lerp(_currentPos, clampedPosition, Time.deltaTime * _moveSpeed);

        // --- UI座標の更新（World → Screen → UI）---
        UpdateCursorUI();
    }

    /// <summary>
    /// World座標をスクリーン座標に変換してUIカーソルを動かす
    /// </summary>
    private void UpdateCursorUI()
    {
        Vector2 screenPoint = Camera.main.WorldToScreenPoint(_currentPos);

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvas.transform as RectTransform,
                screenPoint,
                null,   // Screen Space - Overlay は camera = null
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