using KanKikuchi.AudioManager;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public sealed class ButtonClickSound : MonoBehaviour, IPointerDownHandler, ISubmitHandler
{
    private const string ClickSoundPath = "SE/ConfirmButton";
    private Button _button;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= RegisterSceneButtons;
        SceneManager.sceneLoaded += RegisterSceneButtons;
    }

    private static void RegisterSceneButtons(Scene scene, LoadSceneMode mode)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (Button button in root.GetComponentsInChildren<Button>(true))
            Register(button);
    }

    public static void Register(Button button)
    {
        if (button != null && button.GetComponent<ButtonClickSound>() == null)
            button.gameObject.AddComponent<ButtonClickSound>();
    }

    private void Awake() => _button = GetComponent<Button>();

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left) Play();
    }

    public void OnSubmit(BaseEventData eventData) => Play();

    private void Play()
    {
        if (_button != null && _button.IsActive() && _button.IsInteractable())
            SEManager.Instance.Play(ClickSoundPath);
    }
}
