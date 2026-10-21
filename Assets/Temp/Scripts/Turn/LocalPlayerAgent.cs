using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 自分（この画面を操作している人）の操作役。画面の UI と手札のクリックを、進行中の手続きへつなぐ。
/// 手札は出品者として出品する商品を決めているときだけ使える（それ以外は見る・広げるだけ）。
/// 出品の確定ボタンは出品できる間、手札を広げているときだけ画面の右端に表示する。
/// オークションでは金額を 0 から W/S か ↑/↓ で指定し（長押しで連続して変わる）、入札できる額のときだけ入札ボタンで入札する。
/// ポーズ画面を開いている間は金額のキー操作を受け付けない（メニューの選択と重ならないように）。脱落した後は出品を受け持たない。
/// </summary>
public class LocalPlayerAgent : MonoBehaviour, IPlayerAgent
{
    [SerializeField] private GameManager game;

    [SerializeField] private TurnUI ui;

    [SerializeField] private LocalHandPresenter hand;

    [Tooltip("状態の表示に出す自分の名前")]
    [SerializeField] private PlayerNameDirectory names;

    [Tooltip("開いている間は金額のキー操作を受け付けない")]
    [SerializeField] private PauseMenu pause;

    ListingSession listing;
    AuctionModel auction;
    IPlayerCommands commands;
    int amount;
    readonly KeyRepeat raiseKey = new KeyRepeat();
    readonly KeyRepeat lowerKey = new KeyRepeat();

    public int PlayerNumber { get; private set; } = PlayerRegistry.NoPlayer;

    /// <param name="playerCommands">操作を送る窓口（反映は窓口が行うため、ここでは手続きを直接書き換えない）</param>
    public void Initialize(int playerNumber, IPlayerCommands playerCommands)
    {
        PlayerNumber = playerNumber;
        commands = playerCommands;
    }

    void Awake()
    {
        ui.ConfirmClicked += HandleConfirm;
        ui.BidClicked += HandleBid;
        ui.FoldClicked += HandleFold;
        ui.RaiseClicked += HandleRaise;
        hand.CardClicked += HandleCardClicked;
        game.PlayerDied += HandlePlayerDied;
    }

    void OnDestroy()
    {
        hand.CardClicked -= HandleCardClicked;
        game.PlayerDied -= HandlePlayerDied;
    }

    /// <summary>自分が死んだら操作中の案内とボタンを片付ける（進行状況の表示は TurnFlowController が続ける）</summary>
    void HandlePlayerDied(PlayerState player)
    {
        if (player.PlayerNumber == PlayerNumber)
            EndPhase();
    }

    public void BeginListing(ListingSession session)
    {
        EndPhase();
        if (!game.IsAlive(PlayerNumber))
            return;

        listing = session;
        listing.Changed += RefreshListing;
        RefreshListing();
    }

    public void BeginAuction(AuctionModel model)
    {
        EndPhase();
        if (!model.Participants.Contains(PlayerNumber))
        {
            ui.SetPrompt("オークションを観戦中");
            return;
        }

        auction = model;
        amount = 0;
        hand.Collapse();
        ui.ShowAuction(true);
        RefreshAuction();
    }

    public void EndPhase()
    {
        if (listing != null)
            listing.Changed -= RefreshListing;
        listing = null;
        auction = null;
        ui.ShowConfirm(false, false);
        ui.SetBidInteractable(false);
        ui.ShowRaise(false, false);
        ui.ShowAuction(false);
        ui.SetPrompt(string.Empty);
    }

    void Update()
    {
        if (game.TryGet(PlayerNumber, out PlayerState me))
            ui.SetStatus($"{names.NameOf(PlayerNumber)}  コイン {me.Coins}  落札 {me.Collection.Count}{(me.IsAlive ? string.Empty : "  （観戦中）")}");

        if (listing != null)
        {
            ui.PlaceConfirm(hand.SpreadTopRight);
            ui.RevealConfirm(hand.IsSpread);
        }

        if (auction == null)
            return;

        if (!auction.IsActive(PlayerNumber))
        {
            ui.ShowAuction(false);
            ui.SetBidInteractable(false);
            ui.SetPrompt("オークションを観戦中");
            auction = null;
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && !ui.IsAuctionHidden && !pause.IsOpen)
        {
            int ups = raiseKey.Tick(
                keyboard.wKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame,
                keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed,
                Time.deltaTime);
            int downs = lowerKey.Tick(
                keyboard.sKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame,
                keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed,
                Time.deltaTime);
            amount += (ups - downs) * auction.Listing.BidStep;
        }
        RefreshAuction();
    }

    void HandleCardClicked(HandCard card)
    {
        if (listing == null)
            return;

        if (listing.Goods == null)
            commands.SelectGoods(PlayerNumber, card);
        else if (!listing.CanAttachBuffs && !card.IsGoods)
            hand.Shake(card);
        else
            commands.AttachBuff(PlayerNumber, card);
    }

    void HandleConfirm()
    {
        if (listing != null)
            commands.Confirm(PlayerNumber);
    }

    void HandleRaise(bool raise)
    {
        if (listing != null)
            commands.SetRaise(PlayerNumber, raise);
    }

    void HandleBid()
    {
        if (auction != null)
            commands.Bid(PlayerNumber, amount);
    }

    void HandleFold()
    {
        if (auction != null)
            commands.Fold(PlayerNumber);
    }

    void RefreshListing()
    {
        if (listing == null)
            return;

        ui.ShowConfirm(true, listing.CanConfirm);
        ui.PlaceConfirm(hand.SpreadTopRight);
        ui.RevealConfirm(hand.IsSpread);
        ui.ShowRaise(listing.CanRaise, listing.IsRaised);
        if (listing.Goods == null)
            ui.SetPrompt("手札を広げて、出品するグッズカードを選んでください");
        else if (!listing.CanAttachBuffs)
            ui.SetPrompt($"{listing.Goods.DisplayName}（最低 {listing.MinPrice}）を出品します。横やりでバフデバフカードは付けられません。確定してください");
        else if (!listing.CanRaise)
            ui.SetPrompt($"{listing.Goods.DisplayName}（最低 {listing.MinPrice}）を出品します。バフデバフカードを使うならクリック、決まったら確定");
        else
            ui.SetPrompt($"{listing.Goods.DisplayName}（最低 {listing.MinPrice}）を出品します。増額するか選んで確定");
    }

    /// <summary>金額は 0〜（所持コイン + 前借りできる額）の範囲で自由に指定させ、入札できる額かどうかはボタンの可否で示す</summary>
    void RefreshAuction()
    {
        int highest = auction.HighestSelectable(PlayerNumber);
        amount = Mathf.Clamp(amount, 0, highest);
        ui.SetBidInteractable(auction.CanBid(PlayerNumber, amount));
        ui.SetAuction(amount, $"最低 {auction.Listing.MinPrice} / 最高額 {auction.HighestBid} / 自分 {auction.BidOf(PlayerNumber)} / 所持 {auction.CoinsOf(PlayerNumber)}（{highest} まで入札可）");
        if (highest < auction.Listing.MinPrice)
            ui.SetPrompt("コインが足りないため降りるしかありません（見送り権を使います）");
        else if (!auction.CanAfford(PlayerNumber, amount))
            ui.SetPrompt($"所持コインを超えています。この額で落札して払えないと、見送り権を {auction.ShortfallPenalty} 枚失います");
        else if (auction.IsLeading(PlayerNumber))
            ui.SetPrompt("最高額です。さらに金額を上げて入札することもできます");
        else
            ui.SetPrompt("W/S か ↑/↓ で金額を指定して入札するか、降りるかを選んでください（降りると見送り権を使います）");
    }
}
