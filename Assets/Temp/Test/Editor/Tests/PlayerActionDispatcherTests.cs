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
    public void 出品のやり直しは選んだカードを手札に戻して最初から選び直せる()
    {
        using var table = new VirtualGame(3);
        HandCard goods = TestCards.BlankGoods();
        HandCard buff = TestCards.Effect(CardEffect.Reversal);
        table.Game.AddToHand(Seller, goods);
        table.Game.AddToHand(Seller, buff);
        var dispatcher = new PlayerActionDispatcher(table.Game);
        var session = new ListingSession(table.Game, Seller);
        dispatcher.BeginListing(session);
        dispatcher.SelectGoods(Seller, goods);
        dispatcher.AttachBuff(Seller, buff);
        dispatcher.SetRaise(Seller, true);

        dispatcher.ResetListing(Seller);

        Assert.IsNull(session.Goods);
        Assert.AreEqual(0, session.Buffs.Count);
        Assert.IsFalse(session.IsRaised);
        CollectionAssert.AreEquivalent(new[] { goods, buff }, table.Player(Seller).Hand);

        dispatcher.SelectGoods(Seller, goods);
        Assert.AreSame(goods, session.Goods);
        CollectionAssert.AreEquivalent(new[] { buff }, table.Player(Seller).Hand);
    }

    [Test]
    public void 手札は引いた順に関係なく出品カードが左でエフェクトカードが右に並ぶ()
    {
        using var table = new VirtualGame(3);
        HandCard effectA = TestCards.Effect(CardEffect.Reversal);
        HandCard goodsA = TestCards.BlankGoods();
        HandCard effectB = TestCards.Effect(CardEffect.Reversal);
        HandCard goodsB = TestCards.BlankGoods();

        foreach (HandCard card in new[] { effectA, goodsA, effectB, goodsB })
            table.Game.AddToHand(Seller, card);

        CollectionAssert.AreEqual(new[] { goodsA, goodsB, effectA, effectB }, table.Player(Seller).Hand);
    }

    [Test]
    public void 出品のやり直しは手札を選ぶ前の並びに戻す()
    {
        using var table = new VirtualGame(3);
        HandCard goodsA = TestCards.BlankGoods();
        HandCard goodsB = TestCards.BlankGoods();
        HandCard goodsC = TestCards.BlankGoods();
        HandCard effectA = TestCards.Effect(CardEffect.Reversal);
        HandCard effectB = TestCards.Effect(CardEffect.Reversal);
        HandCard effectC = TestCards.Effect(CardEffect.Reversal);
        foreach (HandCard card in new[] { goodsA, goodsB, goodsC, effectA, effectB, effectC })
            table.Game.AddToHand(Seller, card);
        HandCard[] before = table.Player(Seller).Hand.ToArray();
        var dispatcher = new PlayerActionDispatcher(table.Game);
        var session = new ListingSession(table.Game, Seller);
        dispatcher.BeginListing(session);
        dispatcher.SelectGoods(Seller, goodsB);
        dispatcher.AttachBuff(Seller, effectC);
        dispatcher.AttachBuff(Seller, effectA);

        dispatcher.ResetListing(Seller);

        CollectionAssert.AreEqual(before, table.Player(Seller).Hand);
    }

    [Test]
    public void 出品を確定した後と出品者以外はやり直せない()
    {
        using var table = new VirtualGame(3);
        HandCard goods = TestCards.BlankGoods();
        table.Game.AddToHand(Seller, goods);
        var dispatcher = new PlayerActionDispatcher(table.Game);
        var session = new ListingSession(table.Game, Seller);
        dispatcher.BeginListing(session);
        dispatcher.SelectGoods(Seller, goods);

        dispatcher.ResetListing(2);
        Assert.AreSame(goods, session.Goods);

        dispatcher.Confirm(Seller);
        dispatcher.ResetListing(Seller);
        Assert.AreSame(goods, session.Goods);
        Assert.AreEqual(0, table.Player(Seller).Hand.Count);
    }

    [Test]
    public void オークションの時間切れは最高額の人以外が降りて落札する()
    {
        using var table = new VirtualGame(4);
        var dispatcher = new PlayerActionDispatcher(table.Game);
        var listing = new Listing(Seller, TestCards.BlankGoods(), new HandCard[0], false);
        var auction = new AuctionModel(listing, new[] { 2, 3, 4 }, player => table.Player(player).Coins, 2);
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
        var auction = new AuctionModel(listing, new[] { 2, 3 }, player => table.Player(player).Coins, 2);
        dispatcher.BeginAuction(auction);

        dispatcher.TimeOut(Seller);

        Assert.IsTrue(auction.IsFinished);
        Assert.AreEqual(PlayerRegistry.NoPlayer, auction.Winner);
        Assert.IsFalse(auction.ActivePlayers.Any());
    }
}
