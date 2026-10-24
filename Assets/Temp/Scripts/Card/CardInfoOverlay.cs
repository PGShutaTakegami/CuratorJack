using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// カードの上に重ねる情報表示。カードを薄暗くし、上段に開始価格・カード名・レアリティ、その下に説明を出す。
/// 情報はカードに反映されている CardData から取る。開始価格とレアリティはグッズカードのときだけ出す。
/// 暗幕はカード自身の画像を暗く塗って使い、カードの角の丸みに合わせる。
/// スペシャルカードが付いていれば、カードの上側（SetSpecialBelow で下側）にそのスペシャルカードを出し、
/// カーソルがスペシャルカードに乗っている間はスペシャルカードの名前と説明を出す。
/// </summary>
public class CardInfoOverlay : MonoBehaviour
{
    [SerializeField] private CardManager card;

    [Tooltip("暗幕の形に使うカードの画像")]
    [SerializeField] private Image cardImage;

    [Tooltip("表示・非表示を切り替える部分（暗幕と各テキストの親）")]
    [SerializeField] private GameObject root;

    [Tooltip("カードと同じ形で暗くする画像")]
    [SerializeField] private Image dim;

    [SerializeField] private TMP_Text priceLabel;

    [SerializeField] private TMP_Text nameLabel;

    [SerializeField] private TMP_Text rarityLabel;

    [SerializeField] private TMP_Text descriptionLabel;

    [Tooltip("付いているスペシャルカードを、カードの上に出す画像")]
    [SerializeField] private Image specialPreview;

    [Tooltip("スペシャルカードに乗っている間だけ出す部分（暗幕と各テキストの親）")]
    [SerializeField] private GameObject specialDetail;

    [Tooltip("スペシャルカードと同じ形で暗くする画像")]
    [SerializeField] private Image specialDim;

    [SerializeField] private TMP_Text specialNameLabel;

    [SerializeField] private TMP_Text specialDescriptionLabel;

    public bool IsVisible => root.activeSelf;

    /// <summary>付いているスペシャルカードを出す範囲（表示していないときは非アクティブ）</summary>
    public RectTransform SpecialRect => specialPreview.rectTransform;

    void Awake()
    {
        root.SetActive(false);
        specialPreview.gameObject.SetActive(false);
        specialDetail.SetActive(false);
    }

    public void SetVisible(bool visible)
    {
        if (visible)
            Refresh();
        root.SetActive(visible);
        specialPreview.gameObject.SetActive(visible && specialPreview.sprite != null);
        if (!visible)
            specialDetail.SetActive(false);
    }

    /// <summary>スペシャルカードをカードの下側に出すか（false なら上側）</summary>
    public void SetSpecialBelow(bool below)
    {
        RectTransform rect = specialPreview.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, below ? 0f : 1f);
        rect.pivot = new Vector2(0.5f, below ? 1f : 0f);
        rect.anchoredPosition = Vector2.zero;
    }

    /// <summary>スペシャルカードを表示していて、画面上の位置がその範囲に入っているか</summary>
    public bool SpecialContains(Vector2 screenPoint, Camera eventCamera)
    {
        return specialPreview.gameObject.activeInHierarchy
            && RectTransformUtility.RectangleContainsScreenPoint(specialPreview.rectTransform, screenPoint, eventCamera);
    }

    /// <summary>スペシャルカードの名前と説明を出すか（スペシャルカードを表示しているときだけ出す）</summary>
    public void SetSpecialFocused(bool focused)
    {
        bool show = focused && specialPreview.gameObject.activeSelf;
        if (specialDetail.activeSelf != show)
            specialDetail.SetActive(show);
    }

    void Refresh()
    {
        CardData data = card.Data;
        bool isGoods = card.CardKind == CardManager.CardKinds.GoodCard && data != null;

        dim.sprite = cardImage.sprite;
        nameLabel.text = data != null ? data.Parameter.CardName : card.CardKind.ToString();
        descriptionLabel.text = data != null ? data.Parameter.Discription : string.Empty;
        priceLabel.text = isGoods ? data.Parameter.InitialPrice.ToString() : string.Empty;
        rarityLabel.text = isGoods ? RarityText(data.Rarity) : string.Empty;

        CardData special = card.Special;
        specialPreview.sprite = special != null ? special.Parameter.CardSprite : null;
        specialDim.sprite = specialPreview.sprite;
        specialNameLabel.text = special != null ? special.Parameter.CardName : string.Empty;
        specialDescriptionLabel.text = special != null ? special.Parameter.Discription : string.Empty;
    }

    static string RarityText(GoodsRarity rarity)
    {
        switch (rarity)
        {
            case GoodsRarity.Low: return "低";
            case GoodsRarity.Middle: return "中";
            default: return "高";
        }
    }
}
