using System.Linq;
using TMPro;
using UnityEngine;

/// <summary>
/// 上部のバーに、今の場面を名前入りで表示する。ターン中は「出品者 のターン」、
/// オークション中は残っている参加者を並べて「名前、名前 のオークションバトル中」（降りた人は消す）。
/// 枠に収まるときは中央に置き、収まらないときは、右端から左へ流し、左へ抜けたらまた右端から流す。
/// </summary>
public class TurnHeadlineView : MonoBehaviour
{
    [SerializeField] private TurnFlowController turnFlow;

    [SerializeField] private PlayerNameDirectory names;

    [Tooltip("表示する枠（はみ出した分は隠す）")]
    [SerializeField] private RectTransform viewport;

    [SerializeField] private TMP_Text label;

    [Tooltip("流すときの速さ（1 秒に動く距離）")]
    [SerializeField] private float scrollSpeed = 120f;

    int seller = PlayerRegistry.NoPlayer;
    AuctionModel auction;
    bool isScrolling;

    void Awake()
    {
        label.richText = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        Show(string.Empty);
        turnFlow.TurnStarted += HandleTurnStarted;
        turnFlow.AuctionStarted += HandleAuctionStarted;
        turnFlow.AuctionFinished += HandleAuctionFinished;
    }

    void OnDestroy()
    {
        turnFlow.TurnStarted -= HandleTurnStarted;
        turnFlow.AuctionStarted -= HandleAuctionStarted;
        turnFlow.AuctionFinished -= HandleAuctionFinished;
        StopWatchingAuction();
    }

    void HandleTurnStarted(int player)
    {
        seller = player;
        ShowTurn();
    }

    void HandleAuctionStarted(AuctionModel model)
    {
        StopWatchingAuction();
        auction = model;
        auction.Folded += HandleFolded;
        ShowAuction();
    }

    void HandleAuctionFinished()
    {
        StopWatchingAuction();
        ShowTurn();
    }

    void HandleFolded(int player) => ShowAuction();

    void StopWatchingAuction()
    {
        if (auction != null)
            auction.Folded -= HandleFolded;
        auction = null;
    }

    void ShowTurn()
    {
        Show(seller == PlayerRegistry.NoPlayer ? string.Empty : $"{names.NameOf(seller)} のターン");
    }

    void ShowAuction()
    {
        string bidders = string.Join("、", auction.ActivePlayers.Select(names.NameOf));
        Show(string.IsNullOrEmpty(bidders) ? string.Empty : $"{bidders} のオークションバトル中");
    }

    /// <summary>文字に合わせて幅を決め、枠に収まれば中央に置き、収まらなければ右端から流し始める（流している途中なら位置はそのまま）</summary>
    void Show(string text)
    {
        label.text = text;
        float width = label.GetPreferredValues(text).x;
        RectTransform rect = label.rectTransform;
        rect.sizeDelta = new Vector2(width, rect.sizeDelta.y);

        bool overflows = width > viewport.rect.width;
        if (!overflows)
            rect.anchoredPosition = new Vector2((viewport.rect.width - width) * 0.5f, rect.anchoredPosition.y);
        else if (!isScrolling)
            rect.anchoredPosition = new Vector2(viewport.rect.width, rect.anchoredPosition.y);
        isScrolling = overflows;
    }

    void Update()
    {
        if (!isScrolling)
            return;

        RectTransform rect = label.rectTransform;
        float x = rect.anchoredPosition.x - scrollSpeed * Time.deltaTime;
        if (x < -rect.sizeDelta.x)
            x = viewport.rect.width;
        rect.anchoredPosition = new Vector2(x, rect.anchoredPosition.y);
    }
}
