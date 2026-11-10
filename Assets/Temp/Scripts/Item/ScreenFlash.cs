using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>画面全体を一瞬その色で光らせ、フェードで消す（操作はふさがない）</summary>
[RequireComponent(typeof(Image))]
public class ScreenFlash : MonoBehaviour
{
    [Tooltip("光った瞬間の濃さ")]
    [Range(0f, 1f)]
    [SerializeField] private float peakAlpha = 0.6f;

    [SerializeField] private float fadeSeconds = 0.7f;

    Image image;
    Coroutine running;

    void Awake()
    {
        image = GetComponent<Image>();
        image.raycastTarget = false;
        image.enabled = false;
    }

    public void Flash(Color color)
    {
        if (running != null)
            StopCoroutine(running);
        running = StartCoroutine(Run(color));
    }

    IEnumerator Run(Color color)
    {
        image.enabled = true;
        for (float elapsed = 0f; elapsed < fadeSeconds; elapsed += Time.unscaledDeltaTime)
        {
            float t = elapsed / fadeSeconds;
            image.color = new Color(color.r, color.g, color.b, peakAlpha * (1f - t) * (1f - t));
            yield return null;
        }
        image.enabled = false;
        running = null;
    }
}
