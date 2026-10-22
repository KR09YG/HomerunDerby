using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public sealed class TitleMenuView : MonoBehaviour
{
    [SerializeField] private TitleManager _manager;
    [SerializeField] private RawImage _backgroundView;
    [SerializeField] private Button _startButton;
    [SerializeField] private Button _controlsButton;
    [SerializeField] private Button _creditsButton;
    [SerializeField] private Button _quitButton;
    [SerializeField] private Button _controlsCloseButton;
    [SerializeField] private Button _creditsCloseButton;
    [SerializeField] private GameObject _controlsPanel;
    [SerializeField] private GameObject _creditsPanel;
    [SerializeField] private Image _fade;

    public bool IsPanelOpen => (_creditsPanel != null && _creditsPanel.activeSelf) ||
                               (_controlsPanel != null && _controlsPanel.activeSelf);

    private void Awake()
    {
        if (_manager == null || _backgroundView == null || _startButton == null ||
            _controlsButton == null || _creditsButton == null || _quitButton == null ||
            _controlsCloseButton == null || _creditsCloseButton == null ||
            _controlsPanel == null || _creditsPanel == null || _fade == null)
        {
            Debug.LogError("[Title] タイトルUIの参照が設定されていません。", this);
            enabled = false;
            return;
        }

        _startButton.onClick.AddListener(_manager.StartGame);
        _controlsButton.onClick.AddListener(ShowControls);
        _creditsButton.onClick.AddListener(ShowCredits);
        _quitButton.onClick.AddListener(_manager.QuitGame);
        _controlsCloseButton.onClick.AddListener(Close);
        _creditsCloseButton.onClick.AddListener(Close);
        Close();
        FitBackground();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) Close();
        FitBackground();
    }

    private void OnDestroy()
    {
        if (_fade != null) _fade.DOKill();
    }

    public void FadeToBlack(float duration, Action completed)
    {
        if (_fade == null)
        {
            Debug.LogError("[Title] フェード画像が設定されていません。", this);
            return;
        }

        _fade.DOKill();
        _fade.color = new Color(0f, 0f, 0f, 0f);
        _fade.gameObject.SetActive(true);
        _fade.DOFade(1f, duration).SetUpdate(true).OnComplete(() => completed?.Invoke());
    }

    private void ShowControls() => Show(_controlsPanel);
    private void ShowCredits() => Show(_creditsPanel);

    private void Show(GameObject panel)
    {
        Close();
        panel.SetActive(true);
    }

    private void Close()
    {
        if (_creditsPanel != null) _creditsPanel.SetActive(false);
        if (_controlsPanel != null) _controlsPanel.SetActive(false);
    }

    private void FitBackground()
    {
        if (_backgroundView == null || _backgroundView.texture == null) return;
        RectTransform rect = _backgroundView.rectTransform;
        if (rect.rect.width <= 0f || rect.rect.height <= 0f) return;

        float source = _backgroundView.texture.width / (float)_backgroundView.texture.height;
        float target = rect.rect.width / rect.rect.height;
        _backgroundView.uvRect = source > target
            ? new Rect((1f - target / source) * 0.5f, 0f, target / source, 1f)
            : new Rect(0f, (1f - source / target) * 0.5f, 1f, source / target);
    }
}
