using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// カード一覧の 1 枚。カーソルが乗っている間だけカードを薄暗くして名前と説明を出す（見るだけで操作はできない）。
/// オークション中のスペシャルカードなら、カードの外側を光らせて明滅させる。
/// </summary>
public class SpecialCardCatalogItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image cardImage;

    [Tooltip("カーソルが乗っている間だけ出す部分（暗幕と各テキストの親）")]
    [SerializeField] private GameObject detail;

    [Tooltip("カードと同じ形で暗くする画像")]
    [SerializeField] private Image dim;

    [SerializeField] private TMP_Text nameLabel;

    [SerializeField] private TMP_Text descriptionLabel;

    [Tooltip("オークション中のときだけ出す、カードの外側の光")]
    [SerializeField] private Image glow;

    [Tooltip("光の明滅の速さ（1 秒あたりの角度、ラジアン）")]
    [SerializeField] private float glowPulseSpeed = 3f;

    [Tooltip("明滅で最も暗いときの光の濃さ（最も明るいときは 1）")]
    [SerializeField, Range(0f, 1f)] private float glowMinAlpha = 0.45f;

    public CardData Data { get; private set; }

    /// <summary>カードを表示する。description はカーソルを乗せたときの説明（商品なら価格やレアリティも入れる）</summary>
    public void Bind(CardData data, string description)
    {
        Data = data;
        cardImage.sprite = data.Parameter.CardSprite;
        dim.sprite = cardImage.sprite;
        nameLabel.text = data.Parameter.CardName;
        descriptionLabel.text = description;
        detail.SetActive(false);
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

    public void OnPointerEnter(PointerEventData eventData) => detail.SetActive(true);

    public void OnPointerExit(PointerEventData eventData) => detail.SetActive(false);

    void OnDisable() => detail.SetActive(false);
}
