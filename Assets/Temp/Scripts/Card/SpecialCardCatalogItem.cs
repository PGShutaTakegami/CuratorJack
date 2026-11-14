using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// カード一覧の 1 枚。カードの上の帯に名前、下半分に能力（説明）を常に出す（見るだけで操作はできない）。
/// オークション中のスペシャルカードなら、カードの外側を光らせて明滅させる。
/// </summary>
public class SpecialCardCatalogItem : MonoBehaviour
{
    [SerializeField] private Image cardImage;

    [Tooltip("カードの上に重ねる、カード名と能力")]
    [SerializeField] private CardFaceLabels labels;

    [Tooltip("オークション中のときだけ出す、カードの外側の光")]
    [SerializeField] private Image glow;

    [Tooltip("光の明滅の速さ（1 秒あたりの角度、ラジアン）")]
    [SerializeField] private float glowPulseSpeed = 3f;

    [Tooltip("明滅で最も暗いときの光の濃さ（最も明るいときは 1）")]
    [SerializeField, Range(0f, 1f)] private float glowMinAlpha = 0.45f;

    public CardData Data { get; private set; }

    /// <summary>カードを表示する。description は下半分に出す能力（商品なら価格やレアリティも入れる）</summary>
    public void Bind(CardData data, string description)
    {
        Data = data;
        cardImage.sprite = data.Parameter.CardSprite;
        labels.Show(data.Parameter.CardName, description);
    }

    public void SetHighlighted(bool highlighted) => glow.gameObject.SetActive(highlighted);

    void Update()
    {
        if (!glow.gameObject.activeSelf)
            return;

        float wave = (Mathf.Sin(Time.unscaledTime * glowPulseSpeed) + 1f) * 0.5f;
        Color color = glow.color;
        color.a = Mathf.Lerp(glowMinAlpha, 1f, wave);
        glow.color = color;
    }
}
