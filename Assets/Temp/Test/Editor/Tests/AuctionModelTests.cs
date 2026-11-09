using System.Collections.Generic;
using NUnit.Framework;

/// <summary>オークションのルール（入札できる額・落札者・降りられる条件）を、仮想の入札で確かめる</summary>
public class AuctionModelTests
{
    const int Penalty = 2;

    static AuctionModel Create(Dictionary<int, int> coins, HandCard goods = null, bool drainsSeller = false)
    {
        var listing = new Listing(1, goods ?? TestCards.BlankGoods(), new HandCard[0], false);
        return new AuctionModel(listing, coins.Keys, player => coins[player], Penalty, drainsSeller);
    }

    [Test]
    public void 削れるオークションでは落札額のうち最低出品額を超えた分を削る()
    {
        AuctionModel auction = Create(new Dictionary<int, int> { [2] = 100 }, drainsSeller: true);
        int min = auction.Listing.MinPrice;

        Assert.AreEqual(0, auction.DrainAmount);
        Assert.IsTrue(auction.TryBid(2, min + 10));

        Assert.AreEqual(2, auction.Winner);
        Assert.AreEqual(10, auction.DrainAmount);
    }

    [Test]
    public void 削れないオークションでは多く入札しても削らない()
    {
        AuctionModel auction = Create(new Dictionary<int, int> { [2] = 100 });

        auction.TryBid(2, auction.Listing.MinPrice + 10);

        Assert.AreEqual(2, auction.Winner);
        Assert.AreEqual(0, auction.DrainAmount);
        Assert.AreEqual(0, auction.DrainOf(auction.Listing.MinPrice + 10));
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
    public void 所持コインを超えては入札できない()
    {
        AuctionModel auction = Create(new Dictionary<int, int> { [2] = 20, [3] = 100 });

        Assert.AreEqual(20, auction.HighestSelectable(2));
        Assert.IsFalse(auction.CanBid(2, 21));
        Assert.IsTrue(auction.TryBid(2, 20));
        auction.TryFold(3);

        Assert.AreEqual(2, auction.Winner);
        Assert.IsTrue(auction.CanAfford(2, auction.Price));
    }

    [Test]
    public void 最高額を付けている間は降りられず上回られたら降りられる()
    {
        AuctionModel auction = Create(new Dictionary<int, int> { [2] = 100, [3] = 100, [4] = 100 });

        auction.TryBid(2, 10);
        Assert.IsFalse(auction.CanFold(2));
        Assert.IsFalse(auction.TryFold(2));
        Assert.IsTrue(auction.IsActive(2));

        auction.TryBid(3, 12);
        Assert.IsTrue(auction.TryFold(2));
        Assert.IsFalse(auction.IsActive(2));
    }

    [Test]
    public void 脱落した人は最高額でも抜けられる()
    {
        AuctionModel auction = Create(new Dictionary<int, int> { [2] = 100, [3] = 100, [4] = 100 });

        auction.TryBid(2, 10);
        Assert.IsTrue(auction.Withdraw(2));
        Assert.IsFalse(auction.IsActive(2));
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
