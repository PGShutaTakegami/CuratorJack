using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 画面の端をその色で光らせる（操作はふさがない）。Visible の間はフェードで現れ、ゆっくり明滅するか、バチバチとちらつく。
/// </summary>
[RequireComponent(typeof(Image))]
public class ScreenEdgeGlow : MonoBehaviour
{
    [SerializeField] private Color color = new Color(0.3f, 1f, 0.45f, 1f);

    [Tooltip("明るさの範囲（濃さの倍率）")]
    [SerializeField] private Vector2 alphaRange = new Vector2(0.45f, 0.85f);

    [Tooltip("明滅 1 往復の秒数")]
    [SerializeField] private float pulseSeconds = 1.6f;

    [Tooltip("ゆっくり明滅する代わりに、電気のように不規則にちらつかせる")]
    [SerializeField] private bool flicker;

    [Tooltip("ちらつきの明るさを変える間隔（秒）")]
    [SerializeField] private float flickerInterval = 0.05f;

    [SerializeField] private float fadeSeconds = 0.3f;

    Image image;
    float visibility;
    float elapsed;
    float flickerTimer;
    float flickerAlpha;

    /// <summary>光らせるか（切り替えるとフェードで現れる・消える）</summary>
    public bool Visible { get; set; }

    void Awake()
    {
        image = GetComponent<Image>();
        image.sprite = ScreenEffectSprites.EdgeGlow;
        image.raycastTarget = false;
        image.enabled = false;
    }

    void Update()
    {
        float step = fadeSeconds > 0f ? Time.unscaledDeltaTime / fadeSeconds : 1f;
        visibility = Mathf.MoveTowards(visibility, Visible ? 1f : 0f, step);
        image.enabled = visibility > 0f;
        if (!image.enabled)
            return;

        elapsed += Time.unscaledDeltaTime;
        image.color = new Color(color.r, color.g, color.b, color.a * visibility * Strength());
    }

    float Strength()
    {
        if (!flicker)
            return Mathf.Lerp(alphaRange.x, alphaRange.y, OutlineGlow.Pulse(elapsed, pulseSeconds));

        flickerTimer -= Time.unscaledDeltaTime;
        if (flickerTimer <= 0f)
        {
            flickerTimer = flickerInterval;
            flickerAlpha = Random.value < 0.15f ? alphaRange.y * 1.2f : Random.Range(alphaRange.x, alphaRange.y);
        }
        return flickerAlpha;
    }
}
