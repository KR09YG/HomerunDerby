using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System;

public class Fade: MonoBehaviour
{
    private Image fadeImage;
    public static Fade instance;
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }

        fadeImage = GetComponent<Image>();
    }

    /// <param name="isFadeIn">フェードインかフェードアウトかを指定します</param>
    public static void FadeImage(float duration, bool isFadeIn, Action action = null)
    {
        if (isFadeIn)
        {
            instance.fadeImage.gameObject.SetActive(true);
            instance.fadeImage.DOFade(1f, duration).SetEase(Ease.Linear).OnComplete(() =>
            {
                action?.Invoke();
            });
        }
        else
        {
            instance.fadeImage.DOFade(0f, duration).SetEase(Ease.Linear).OnComplete(() =>
            {
                instance.fadeImage.gameObject.SetActive(false);
                action?.Invoke();
            });
        }
    }


}
