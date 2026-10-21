using System.Collections.Generic;
using NUnit.Framework;

/// <summary>オークションのルール（入札できる額・落札者・前借り）を、仮想の入札で確かめる</summary>
public class AuctionModelTests
{
    const int Overdraft = 10;
    const int Penalty = 2;

    static AuctionModel Create(Dictionary<int, int> coins, HandCard goods = null)
    {
        var listing = new Listing(1, goods ?? TestCards.BlankGoods(), new HandCard[0], false);
        return new AuctionModel(listing, coins.Keys, player => coins[player], Overdraft, Penalty);
    }

    [Test]
    public void 最高額より上でないと入札できない()
    {
        AuctionModel auction = Create(new Dictionary<int, int> { [2] = 100, [3] = 100 });

        Assert.IsTrue(auction.TryBid(2, 10));
        Assert.IsFalse(auction.TryBid(3, 10));
        Assert.IsTrue(auction.TryBid(3, 11));
        Assert.AreEqual(11, auction.HighestBid);
    }

    [Test]
    public void 最高額の人以外が降りたら最高額で落札する()
    {
        AuctionModel auction = Create(new Dictionary<int, int> { [2] = 100, [3] = 100, [4] = 100 });

        auction.TryBid(2, 10);
        auction.TryBid(3, 12);
        auction.TryFold(2);
        Assert.IsFalse(auction.IsFinished);
        auction.TryFold(4);

        Assert.IsTrue(auction.IsFinished);
        Assert.AreEqual(3, auction.Winner);
        Assert.AreEqual(12, auction.Price);
    }

    [Test]
    public void 残り1人でも入札していなければ落札にならない()
    {
        AuctionModel auction = Create(new Dictionary<int, int> { [2] = 100, [3] = 100 });

        auction.TryFold(2);

        Assert.IsFalse(auction.IsFinished);
        Assert.AreEqual(PlayerRegistry.NoPlayer, auction.Winner);
    }

    [Test]
    public void 全員が降りたら落札者なし()
    {
        AuctionModel auction = Create(new Dictionary<int, int> { [2] = 100, [3] = 100 });

        auction.TryFold(2);
        auction.TryFold(3);

        Assert.IsTrue(auction.IsFinished);
        Assert.AreEqual(PlayerRegistry.NoPlayer, auction.Winner);
        Assert.AreEqual(0, auction.Price);
    }

    [Test]
    public void 所持コインより前借りの分まで多く入札できるが払えない()
    {
        AuctionModel auction = Create(new Dictionary<int, int> { [2] = 20, [3] = 100 });

        Assert.IsFalse(auction.CanBid(2, 20 + Overdraft + 1));
        Assert.IsTrue(auction.TryBid(2, 20 + Overdraft));
        auction.TryFold(3);

        Assert.AreEqual(2, auction.Winner);
        Assert.IsFalse(auction.CanAfford(2, auction.Price));
    }

    [Test]
    public void 最低出品額より安くは入札できない()
    {
        HandCard goods = TestCards.Goods(GoodsRarity.Middle);
        AuctionModel auction = Create(new Dictionary<int, int> { [2] = 100, [3] = 100 }, goods);
        int minPrice = auction.Listing.MinPrice;

        Assert.IsFalse(auction.CanBid(2, minPrice - 1));
        Assert.IsTrue(auction.CanBid(2, minPrice));
    }

    [Test]
    public void 降りた人と終わった後は入札できない()
    {
        AuctionModel auction = Create(new Dictionary<int, int> { [2] = 100, [3] = 100 });

        auction.TryBid(2, 5);
        auction.TryFold(3);

        Assert.IsFalse(auction.TryBid(3, 50));
        Assert.IsFalse(auction.TryBid(2, 50));
    }
}
