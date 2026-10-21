using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// インフォメーションマークを押すと、画面中央にカードの一覧を開く。ウィンドウの外を押すと閉じる。
/// 上の切り替えで「商品」「エフェクト」「スペシャル」の一覧を選び、それぞれの山札に登録されたカードを横一列に並べる。
/// 入りきらない分は、ウィンドウを押したまま左右に動かすか、左右の方向キーを押している間スライドして見る。
/// オークション中のスペシャルカードは外側を光らせ、開いたときは「スペシャル」にしてそのカードが見える位置までスライドする。
/// </summary>
public class SpecialCardCatalog : MonoBehaviour
{
    enum Page
    {
        Goods,
        Effects,
        Specials
    }

    [SerializeField] private GoodsCardDeck goodsDeck;

    [SerializeField] private EffectCardDeck effectDeck;

    [SerializeField] private SpecialCardDeck deck;

    [Tooltip("一覧を開くインフォメーションマーク")]
    [SerializeField] private Button openButton;

    [Tooltip("開いている間だけ表示する部分（ウィンドウ外の背景とウィンドウの親）")]
    [SerializeField] private GameObject root;

    [Tooltip("ウィンドウ外の背景。押すと閉じる")]
    [SerializeField] private Button backdrop;

    [Tooltip("商品の一覧に切り替える")]
    [SerializeField] private Button goodsTab;

    [Tooltip("エフェクトの一覧に切り替える")]
    [SerializeField] private Button effectTab;

    [Tooltip("スペシャルの一覧に切り替える")]
    [SerializeField] private Button specialTab;

    [SerializeField] private Color selectedTabColor = new Color(0.85f, 0.68f, 0.35f, 1f);

    [SerializeField] private Color tabColor = new Color(0.18f, 0.15f, 0.12f, 1f);

    [Tooltip("カードを横に並べてスライドさせる部分")]
    [SerializeField] private ScrollRect scroll;

    [Tooltip("並べるカードの見本（実行時は非表示）")]
    [SerializeField] private SpecialCardCatalogItem itemTemplate;

    [Tooltip("方向キーを押している間にスライドする速さ（1 秒あたりの距離）")]
    [SerializeField] private float keyScrollSpeed = 1200f;

    readonly Dictionary<Page, List<SpecialCardCatalogItem>> pages = new Dictionary<Page, List<SpecialCardCatalogItem>>();
    Page current = Page.Goods;
    CardData auctioned;
    bool built;

    void Awake()
    {
        itemTemplate.gameObject.SetActive(false);
        root.SetActive(false);
        openButton.onClick.AddListener(Open);
        backdrop.onClick.AddListener(Close);
        goodsTab.onClick.AddListener(ShowGoods);
        effectTab.onClick.AddListener(ShowEffects);
        specialTab.onClick.AddListener(ShowSpecials);
    }

    void OnDestroy()
    {
        openButton.onClick.RemoveListener(Open);
        backdrop.onClick.RemoveListener(Close);
        goodsTab.onClick.RemoveListener(ShowGoods);
        effectTab.onClick.RemoveListener(ShowEffects);
        specialTab.onClick.RemoveListener(ShowSpecials);
    }

    void ShowGoods() => Show(Page.Goods);

    void ShowEffects() => Show(Page.Effects);

    void ShowSpecials() => Show(Page.Specials);

    public void Open()
    {
        if (!built)
            Build();
        root.SetActive(true);
        Show(auctioned != null ? Page.Specials : current);

        SpecialCardCatalogItem highlighted = auctioned != null ? pages[Page.Specials].Find(item => item.Data == auctioned) : null;
        if (highlighted != null)
            ScrollTo(highlighted);
    }

    public void Close()
    {
        root.SetActive(false);
    }

    /// <summary>オークション中のスペシャルカードを光らせる。null で光を消す</summary>
    public void SetAuctioned(CardData special)
    {
        auctioned = special;
        if (!built)
            return;

        foreach (SpecialCardCatalogItem item in pages[Page.Specials])
            item.SetHighlighted(special != null && item.Data == special);
    }

    /// <summary>page の一覧だけを表示し、切り替えの色を変えて、左端から見せる</summary>
    void Show(Page page)
    {
        current = page;
        foreach (KeyValuePair<Page, List<SpecialCardCatalogItem>> entry in pages)
        {
            foreach (SpecialCardCatalogItem item in entry.Value)
                item.gameObject.SetActive(entry.Key == page);
        }

        SetTabColor(goodsTab, page == Page.Goods);
        SetTabColor(effectTab, page == Page.Effects);
        SetTabColor(specialTab, page == Page.Specials);

        scroll.StopMovement();
        Canvas.ForceUpdateCanvases();
        scroll.horizontalNormalizedPosition = 0f;
    }

    void SetTabColor(Button tab, bool selected) => tab.image.color = selected ? selectedTabColor : tabColor;

    void Update()
    {
        if (!root.activeSelf)
            return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        float direction = (keyboard.rightArrowKey.isPressed ? 1f : 0f) - (keyboard.leftArrowKey.isPressed ? 1f : 0f);
        if (direction != 0f)
            ScrollBy(direction * keyScrollSpeed * Time.unscaledDeltaTime);
    }

    /// <summary>横に distance だけスライドする（端で止まる）</summary>
    void ScrollBy(float distance)
    {
        float hidden = scroll.content.rect.width - scroll.viewport.rect.width;
        if (hidden <= 0f)
            return;

        scroll.StopMovement();
        scroll.horizontalNormalizedPosition = Mathf.Clamp01(scroll.horizontalNormalizedPosition + distance / hidden);
    }

    /// <summary>item がビューの中央に来るようにスライドする（端で止まる）</summary>
    void ScrollTo(SpecialCardCatalogItem item)
    {
        float hidden = scroll.content.rect.width - scroll.viewport.rect.width;
        if (hidden <= 0f)
            return;

        RectTransform rect = (RectTransform)item.transform;
        float left = rect.localPosition.x - rect.rect.width * rect.pivot.x;
        float center = left + rect.rect.width * 0.5f + scroll.content.rect.width * scroll.content.pivot.x;
        scroll.horizontalNormalizedPosition = Mathf.Clamp01((center - scroll.viewport.rect.width * 0.5f) / hidden);
    }

    void Build()
    {
        built = true;
        pages[Page.Goods] = BuildPage(goodsDeck.Goods, GoodsDescription);
        pages[Page.Effects] = BuildPage(effectDeck.Effects, card => card.Parameter.Discription);
        pages[Page.Specials] = BuildPage(deck.Specials, card => card.Parameter.Discription);
        SetAuctioned(auctioned);
    }

    List<SpecialCardCatalogItem> BuildPage(IReadOnlyList<CardData> cards, System.Func<CardData, string> describe)
    {
        var items = new List<SpecialCardCatalogItem>();
        foreach (CardData card in cards)
        {
            if (card == null)
                continue;

            SpecialCardCatalogItem item = Instantiate(itemTemplate, scroll.content);
            item.name = card.name;
            item.Bind(card, describe(card));
            item.SetHighlighted(false);
            items.Add(item);
        }
        return items;
    }

    /// <summary>商品の説明：開始価格とレアリティを先に出し、説明があれば続ける</summary>
    static string GoodsDescription(CardData card)
    {
        string text = $"開始価格 {card.Parameter.InitialPrice}\nレアリティ {RarityText(card.Rarity)}";
        return string.IsNullOrEmpty(card.Parameter.Discription) ? text : $"{text}\n\n{card.Parameter.Discription}";
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
