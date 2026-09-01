using UnityEngine;
using UnityEngine.SceneManagement;

public class TiteManager : MonoBehaviour
{
    [SerializeField] private float _fadeDuration = 0.5f; // フェードの時間(s)
    [SerializeField] private string _gameSceneName = "Homerun"; // ゲームシーンの名前
    public void StartGame()
    {
        Fade.FadeImage(_fadeDuration, true, () =>
        {
            SceneManager.LoadScene(_gameSceneName);
        });
    }
}
