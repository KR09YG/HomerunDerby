using KanKikuchi.AudioManager;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(TitleMenuView))]
public class TitleManager : MonoBehaviour
{
    [SerializeField] private float _fadeDuration = 0.5f;
    [SerializeField] private string _gameSceneName = "WorldSelect";
    [SerializeField] private float _bgmVolumeRate = 0.3f;
    private bool _starting;

    private void Awake()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        BGMManager bgm = BGMManager.Instance;
        if (!bgm.GetCurrentAudioNames().Contains("world_anthem_spirits_40sec_master"))
            bgm.Play("BGM/world_anthem_spirits_40sec_master", volumeRate: _bgmVolumeRate);
    }
    public void StartGame()
    {
        if (_starting || GetComponent<TitleMenuView>().IsPanelOpen) return;
        _starting = true;
        GetComponent<TitleMenuView>().FadeToBlack(_fadeDuration,
            () => SceneManager.LoadScene(_gameSceneName));
    }

    public void QuitGame() => Application.Quit();
}
