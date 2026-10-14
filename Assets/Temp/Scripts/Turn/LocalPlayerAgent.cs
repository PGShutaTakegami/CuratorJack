using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 自分（この画面を操作している人）の操作役。画面の UI と手札のクリックを、進行中の手続きへつなぐ。
/// 手札は出品者として出品する商品を決めているときだけ使える（それ以外は見る・広げるだけ）。
/// 出品の確定ボタンは出品できる間、手札を広げているときだけ画面の右端に表示する。
/// オークションでは W/S か ↑/↓ で金額を変え、入札ボタンで入札する。
/// </summary>
public class LocalPlayerAgent : MonoBehaviour, IPlayerAgent
{
    [SerializeField] private GameManager game;

    [SerializeField] private TurnUI ui;

    [SerializeField] private LocalHandPresenter hand;

    ListingSession listing;
    PurchaseSession purchase;
    AuctionModel auction;
    int amount;

    public int PlayerNumber { get; private set; } = PlayerRegistry.NoPlayer;

    public void Initialize(int playerNumber)
    {
        PlayerNumber = playerNumber;
    }

    void Awake()
    {
        ui.ConfirmClicked += () => listing?.TryConfirm();
        ui.BidClicked += () => auction?.TryBid(PlayerNumber, amount);
        ui.BuyClicked += () => Decide(true);
        ui.PassClicked += () => Decide(false);
        ui.FoldClicked += HandleFold;
        ui.RaiseClicked += raise => listing?.TrySetRaise(raise);
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
        listing = session;
        listing.Changed += RefreshListing;
        RefreshListing();
    }

    public void BeginPurchase(PurchaseSession session)
    {
        EndPhase();
        purchase = session;
        bool canBuy = session.CanBuy(PlayerNumber);
        hand.Collapse();
        ui.ShowPurchase(true, canBuy);
        ui.SetPrompt(canBuy
            ? $"{session.Listing.Goods.DisplayName}（最低 {session.Listing.MinPrice}）を購入しますか？"
            : $"{session.Listing.Goods.DisplayName}（最低 {session.Listing.MinPrice}）はコインが足りないため見送るしかありません");
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
        amount = DefaultAmount();
        ui.ShowAuction(true);
        RefreshAuction();
    }

    public void EndPhase()
    {
        if (listing != null)
            listing.Changed -= RefreshListing;
        listing = null;
        purchase = null;
        auction = null;
        ui.ShowConfirm(false, false);
        ui.SetBidInteractable(false);
        ui.ShowRaise(false, false);
        ui.ShowPurchase(false, false);
        ui.ShowAuction(false);
        ui.SetPrompt(string.Empty);
    }

    void Update()
    {
        if (game.TryGet(PlayerNumber, out PlayerState me))
            ui.SetStatus($"Player{PlayerNumber}  コイン {me.Coins}  見送り権 {me.PassRights}  落札 {me.Collection.Count}{(me.IsAlive ? string.Empty : "  （観戦中）")}");

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
        if (keyboard != null)
        {
            int step = auction.Listing.BidStep;
            if (keyboard.wKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame)
                amount += step;
            if (keyboard.sKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame)
                amount -= step;
        }
        RefreshAuction();
    }

    void HandleCardClicked(HandCard card)
    {
        if (listing == null)
            return;

        if (listing.Goods == null)
            listing.TrySelectGoods(card);
        else
            listing.TryAttachBuff(card);
    }

    void Decide(bool buy)
    {
        if (purchase == null || !purchase.TryDecide(PlayerNumber, buy) || !game.IsAlive(PlayerNumber))
            return;

        ui.ShowPurchase(false, false);
        ui.SetPrompt(buy ? "購入者がそろうのを待っています" : "見送りました");
        purchase = null;
    }

    void HandleFold()
    {
        auction?.TryFold(PlayerNumber);
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
        else if (!listing.CanRaise)
            ui.SetPrompt($"{listing.Goods.DisplayName}（最低 {listing.MinPrice}）を出品します。バフデバフカードを使うならクリック、決まったら確定");
        else
            ui.SetPrompt($"{listing.Goods.DisplayName}（最低 {listing.MinPrice}）を出品します。増額するか選んで確定");
    }

    int DefaultAmount() => SelectableFloor();

    /// <summary>選べる金額の下限。最低出品額・自分の前回の額に加え、今の最高額を 1 刻み上回る額（所持コインが上限）。</summary>
    int SelectableFloor()
    {
        int floor = auction.LowestSelectable(PlayerNumber);
        if (auction.HighestBid > 0)
            floor = Mathf.Max(floor, auction.HighestBid + auction.Listing.BidStep);
        return Mathf.Min(floor, auction.CoinsOf(PlayerNumber));
    }

    void RefreshAuction()
    {
        amount = Mathf.Clamp(amount, SelectableFloor(), Mathf.Max(SelectableFloor(), auction.CoinsOf(PlayerNumber)));
        ui.SetBidInteractable(auction.CanBid(PlayerNumber, amount));
        ui.SetAuction(amount, $"最低 {auction.Listing.MinPrice} / 最高額 {auction.HighestBid} / 自分 {auction.BidOf(PlayerNumber)} / 所持 {auction.CoinsOf(PlayerNumber)}");
        ui.SetPrompt("W/S か ↑/↓ で金額を変えて入札。降りると見送り権を使います");
    }
}
