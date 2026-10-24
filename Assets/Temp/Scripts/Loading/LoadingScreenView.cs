using UnityEngine;

/// <summary>
/// LoadingScene のキャンバス。表示はすぐに切り替え、非表示はフェードアウトして下の画面を見せる。
/// </summary>
[RequireComponent(typeof(Canvas), typeof(CanvasGroup))]
public class LoadingScreenView : MonoBehaviour
{
    const float MaxFadeStep = 1f / 30f;

    [SerializeField] private Canvas canvas;

    [SerializeField] private CanvasGroup canvasGroup;

    [SerializeField] private float fadeOutDuration = 0.5f;

    bool visible;

    void Awake()
    {
        Apply(0f);
    }

    public void SetVisible(bool value)
    {
        visible = value;
        if (visible)
            Apply(1f);
    }

    void Update()
    {
        if (visible || canvasGroup.alpha <= 0f)
            return;

        float step = Mathf.Min(Time.unscaledDeltaTime, MaxFadeStep) / fadeOutDuration;
        Apply(Mathf.MoveTowards(canvasGroup.alpha, 0f, step));
    }

    void Apply(float alpha)
    {
        canvasGroup.alpha = alpha;
        canvasGroup.blocksRaycasts = visible;
        canvas.enabled = alpha > 0f;
    }
}
