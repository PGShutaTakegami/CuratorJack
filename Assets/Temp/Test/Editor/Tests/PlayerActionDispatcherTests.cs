using System.Linq;
using NUnit.Framework;

/// <summary>出品・オークションの待ち時間切れを、仮想の対戦卓で起きたことにして結果を確かめる</summary>
public class PlayerActionDispatcherTests
{
    const int Seller = 1;

    [Test]
    public void 出品の時間切れはランダムなグッズで確定する()
    {
        using var table = new VirtualGame(3);
        table.Game.AddToHand(Seller, TestCards.Effect(CardEffect.Reversal));
        table.Game.AddToHand(Seller, TestCards.BlankGoods());
        var dispatcher = new PlayerActionDispatcher(table.Game);
        var session = new ListingSession(table.Game, Seller);
        dispatcher.BeginListing(session);

        dispatcher.TimeOut(Seller);

        Assert.IsTrue(session.IsConfirmed);
        Assert.IsTrue(session.Goods.IsGoods);
        Assert.AreEqual(0, session.Buffs.Count);
        Assert.IsFalse(table.Player(Seller).HasGoods);
    }

    [Test]
    public void 出品の時間切れは選んでいたグッズをそのまま確定する()
    {
        using var table = new VirtualGame(3);
        HandCard chosen = TestCards.BlankGoods();
        table.Game.AddToHand(Seller, chosen);
        table.Game.AddToHand(Seller, TestCards.BlankGoods());
        var dispatcher = new PlayerActionDispatcher(table.Game);
        var session = new ListingSession(table.Game, Seller);
        dispatcher.BeginListing(session);
        dispatcher.SelectGoods(Seller, chosen);

        dispatcher.TimeOut(Seller);

        Assert.IsTrue(session.IsConfirmed);
        Assert.AreSame(chosen, session.Goods);
    }

    [Test]
    public void オークションの時間切れは最高額の人以外が降りて落札する()
    {
        using var table = new VirtualGame(4);
        var dispatcher = new PlayerActionDispatcher(table.Game);
        var listing = new Listing(Seller, TestCards.BlankGoods(), new HandCard[0], false);
        var auction = new AuctionModel(listing, new[] { 2, 3, 4 }, player => table.Player(player).Coins, 10, 2);
        dispatcher.BeginAuction(auction);
        dispatcher.Bid(2, 5);
        dispatcher.Bid(3, 8);

        dispatcher.TimeOut(Seller);

        Assert.IsTrue(auction.IsFinished);
        Assert.AreEqual(3, auction.Winner);
        Assert.AreEqual(8, auction.Price);
    }

    [Test]
    public void 誰も入札していないオークションの時間切れは落札者なし()
    {
        using var table = new VirtualGame(3);
        var dispatcher = new PlayerActionDispatcher(table.Game);
        var listing = new Listing(Seller, TestCards.BlankGoods(), new HandCard[0], false);
        var auction = new AuctionModel(listing, new[] { 2, 3 }, player => table.Player(player).Coins, 10, 2);
        dispatcher.BeginAuction(auction);

        dispatcher.TimeOut(Seller);

        Assert.IsTrue(auction.IsFinished);
        Assert.AreEqual(PlayerRegistry.NoPlayer, auction.Winner);
        Assert.IsFalse(auction.ActivePlayers.Any());
    }
}
