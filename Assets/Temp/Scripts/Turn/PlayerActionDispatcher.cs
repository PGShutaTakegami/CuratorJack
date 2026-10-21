using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 操作役から届いた操作を、今の手続き（出品・オークション）へ反映する。
/// 手続きには始まった順に番号を振り、操作にはその番号を付けて送る。
/// 経路がなければ（SinglePlay）送った操作をすぐ反映し、経路があれば（ネットワーク）全員の画面へ配られて同じ順に届いた操作を反映する。
/// まだ始まっていない手続き宛ての操作は始まるまで取っておき、終わった手続き宛ての操作は捨てる。
/// リタイアは手続きの外でも送れる。経路があれば、手続きの外で送った・届いたリタイアは次の手続きの始まりに反映し、全員の画面で同じ時点で脱落させる。
/// 時間切れも操作として同じ順に反映する。出品で選ぶグッズは送る側（部屋主）が決めて送り、全員の画面で同じグッズになるようにする。
/// </summary>
public sealed class PlayerActionDispatcher : IPlayerCommands, IPhaseTimeouts
{
    const int NoCard = -1;

    readonly GameManager game;
    readonly List<PlayerAction> pending = new List<PlayerAction>();
    readonly Random timeoutRandom = new Random();
    IPlayerActionChannel channel;
    int phase;
    ListingSession listing;
    HandCard[] sellerHand = Array.Empty<HandCard>();
    AuctionModel auction;

    public PlayerActionDispatcher(GameManager game)
    {
        this.game = game;
    }

    bool IsOpen => listing != null || auction != null;

    /// <summary>操作を全員の画面へ配る経路をつなぐ（null なら送った操作をすぐ反映する）</summary>
    public void Connect(IPlayerActionChannel actionChannel)
    {
        channel = actionChannel;
    }

    public void BeginListing(ListingSession session)
    {
        Begin();
        listing = session;
        sellerHand = game.TryGet(session.Seller, out PlayerState seller) ? seller.Hand.ToArray() : Array.Empty<HandCard>();
        ApplyPending();
    }

    public void BeginAuction(AuctionModel model)
    {
        Begin();
        auction = model;
        ApplyPending();
    }

    public void EndPhase()
    {
        listing = null;
        sellerHand = Array.Empty<HandCard>();
        auction = null;
    }

    /// <summary>全員の画面へ配られた操作を受け取る</summary>
    public void Receive(PlayerAction action)
    {
        if (action.Phase > phase)
            pending.Add(action);
        else if (action.Phase == phase && IsOpen)
            Apply(action);
        else if (action.Kind == PlayerActionKind.Retire)
            pending.Add(new PlayerAction(phase + 1, action.Player, action.Kind, action.Value));
    }

    public void SelectGoods(int player, HandCard card) => Submit(player, PlayerActionKind.SelectGoods, Array.IndexOf(sellerHand, card));

    public void AttachBuff(int player, HandCard card) => Submit(player, PlayerActionKind.AttachBuff, Array.IndexOf(sellerHand, card));

    public void SetRaise(int player, bool raise) => Submit(player, PlayerActionKind.SetRaise, raise ? 1 : 0);

    public void Confirm(int player) => Submit(player, PlayerActionKind.Confirm, 0);

    public void Bid(int player, int amount) => Submit(player, PlayerActionKind.Bid, amount);

    public void Fold(int player) => Submit(player, PlayerActionKind.Fold, 0);

    public void Retire(int player)
    {
        if (channel == null)
            game.Retire(player);
        else
            channel.Send(new PlayerAction(IsOpen ? phase : phase + 1, player, PlayerActionKind.Retire, 0));
    }

    public void TimeOut(int sender)
    {
        Submit(sender, PlayerActionKind.Timeout, listing != null && listing.Goods == null ? RandomGoodsIndex() : NoCard);
    }

    int RandomGoodsIndex()
    {
        int[] goods = Enumerable.Range(0, sellerHand.Length).Where(index => sellerHand[index].IsGoods).ToArray();
        return goods.Length == 0 ? NoCard : goods[timeoutRandom.Next(goods.Length)];
    }

    void Begin()
    {
        EndPhase();
        phase++;
    }

    void Submit(int player, PlayerActionKind kind, int value)
    {
        if (!IsOpen)
            return;

        var action = new PlayerAction(phase, player, kind, value);
        if (channel == null)
            Apply(action);
        else
            channel.Send(action);
    }

    void ApplyPending()
    {
        PlayerAction[] ready = pending.Where(action => action.Phase == phase).ToArray();
        pending.RemoveAll(action => action.Phase <= phase);
        foreach (PlayerAction action in ready)
            Apply(action);
    }

    void Apply(PlayerAction action)
    {
        switch (action.Kind)
        {
            case PlayerActionKind.SelectGoods:
                if (TryGetSellerCard(action, out HandCard goods))
                    listing.TrySelectGoods(goods);
                break;
            case PlayerActionKind.AttachBuff:
                if (TryGetSellerCard(action, out HandCard buff))
                    listing.TryAttachBuff(buff);
                break;
            case PlayerActionKind.SetRaise:
                if (IsSeller(action.Player))
                    listing.TrySetRaise(action.Value != 0);
                break;
            case PlayerActionKind.Confirm:
                if (IsSeller(action.Player))
                    listing.TryConfirm();
                break;
            case PlayerActionKind.Bid:
                auction?.TryBid(action.Player, action.Value);
                break;
            case PlayerActionKind.Fold:
                auction?.TryFold(action.Player);
                break;
            case PlayerActionKind.Retire:
                game.Retire(action.Player);
                break;
            case PlayerActionKind.Timeout:
                ApplyTimeout(action.Value);
                break;
        }
    }

    void ApplyTimeout(int goodsIndex)
    {
        if (listing != null)
        {
            if (listing.Goods == null && goodsIndex >= 0 && goodsIndex < sellerHand.Length)
                listing.TrySelectGoods(sellerHand[goodsIndex]);
            listing.TryConfirm();
        }
        else if (auction != null)
        {
            foreach (int player in auction.ActivePlayers.Where(player => !auction.IsLeading(player)).ToArray())
                auction.TryFold(player);
        }
    }

    bool IsSeller(int player) => listing != null && listing.Seller == player;

    bool TryGetSellerCard(PlayerAction action, out HandCard card)
    {
        bool valid = IsSeller(action.Player) && action.Value >= 0 && action.Value < sellerHand.Length;
        card = valid ? sellerHand[action.Value] : null;
        return valid;
    }
}
