using UnityEngine;
using UnityEngine.SceneManagement;
using Cinemachine;
using Cysharp.Threading.Tasks;
using TMPro;

public class Result : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _scoreText;
    [SerializeField] private float _fadeDuration = 0.5f; // フェードの時間(s)
    [SerializeField] private CinemachineVirtualCamera _gameCamera; // ゲーム用カメラ
    [SerializeField] private CinemachineVirtualCamera _resultCamera; // 結果表示用カメラ
    [SerializeField] private GameObject _hideUI;
    private void OnEnable()
    {
        _scoreText.text = "";
        _gameCamera.Priority = 0;
        _resultCamera.Priority = 11;
        _hideUI.SetActive(false);
    }

    public void ToTitle()
    {
        Fade.FadeImage(_fadeDuration, true, () =>
        {
            SceneManager.LoadScene("Title");
        });
    }
}
