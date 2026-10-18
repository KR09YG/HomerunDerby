using KanKikuchi.AudioManager;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TitleManager : MonoBehaviour
{
    [SerializeField] private float _fadeDuration = 0.5f;
    [SerializeField] private string _gameSceneName = "WorldSelect";
    [SerializeField] private float _bgmVolumeRate = 0.3f;

    private void Awake()
    {
        BGMManager.Instance.Play(BGMPath.TITLE, volumeRate: _bgmVolumeRate);
    }
    public void StartGame()
    {
        SEManager.Instance.Play(SEPath.BATTING_HIT);
        Fade.FadeImage(_fadeDuration, true, () =>
        {
            SceneManager.LoadScene(_gameSceneName);
        });
    }
}
