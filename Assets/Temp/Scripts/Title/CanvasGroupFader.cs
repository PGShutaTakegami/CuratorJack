using System;
using UnityEngine;

/// <summary>
/// CanvasGroup の透明度をフェードさせ、完了した時点で通知する。
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class CanvasGroupFader : MonoBehaviour
{
    [SerializeField] private float fadeOutDuration = 0.5f;

    [SerializeField] private float fadeInDuration = 0.5f;

    [Tooltip("有効時、表示開始時に透明から表示までフェードインする")]
    [SerializeField] private bool fadeInOnStart;

    CanvasGroup canvasGroup;
    float startAlpha;
    float targetAlpha;
    float duration;
    float elapsed;
    bool isFading;
    Action onCompleted;

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();

        // 最初の描画で一瞬表示されないよう、Start より前に透明にしておく
        if (fadeInOnStart)
            canvasGroup.alpha = 0f;
    }

    void Start()
    {
        if (fadeInOnStart)
            FadeIn(null);
    }

    /// <summary>
    /// 透明度を 0 までフェードさせ、完全に消えた後に onCompleted を呼ぶ
    /// </summary>
    public void FadeOut(Action onCompleted)
    {
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        BeginFade(0f, fadeOutDuration, onCompleted);
    }

    /// <summary>
    /// 透明度を 1 までフェードさせ、完全に表示された後に onCompleted を呼ぶ
    /// </summary>
    public void FadeIn(Action onCompleted)
    {
        BeginFade(1f, fadeInDuration, () =>
        {
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            onCompleted?.Invoke();
        });
    }

    void BeginFade(float target, float fadeDuration, Action completed)
    {
        startAlpha = canvasGroup.alpha;
        targetAlpha = target;
        duration = fadeDuration;
        elapsed = 0f;
        onCompleted = completed;
        isFading = true;

        if (duration <= 0f)
            Complete();
    }

    void Update()
    {
        if (!isFading)
            return;

        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);
        canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);

        if (t >= 1f)
            Complete();
    }

    void Complete()
    {
        canvasGroup.alpha = targetAlpha;
        isFading = false;
        Action completed = onCompleted;
        onCompleted = null;
        completed?.Invoke();
    }
}
