using NUnit.Framework;

/// <summary>出品の最低出品額（増額・投げ売り）と入札の刻みを確かめる</summary>
public class ListingTests
{
    [Test]
    public void 増額は効果カードを付けたときだけかかる()
    {
        HandCard goods = TestCards.Goods(GoodsRarity.Low);
        int basePrice = Listing.InitialPriceOf(goods);

        var withoutBuff = new Listing(1, goods, new HandCard[0], isRaised: true);
        var withBuff = new Listing(1, goods, new[] { TestCards.Effect(CardEffect.Reversal) }, isRaised: true);

        Assert.AreEqual(basePrice, withoutBuff.MinPrice);
        Assert.AreEqual(basePrice + Listing.PriceRaise, withBuff.MinPrice);
    }

    [Test]
    public void 投げ売りで最低出品額が下がり0未満にはならない()
    {
        Assert.AreEqual(7, Listing.PriceOf(10, 0, 3));
        Assert.AreEqual(0, Listing.PriceOf(3, 0, 5));
    }

    [TestCase(GoodsRarity.Low, 1)]
    [TestCase(GoodsRarity.Middle, 2)]
    [TestCase(GoodsRarity.Highest, 5)]
    public void 入札の刻みはレアリティで決まる(GoodsRarity rarity, int step)
    {
        var listing = new Listing(1, TestCards.Goods(rarity), new HandCard[0], false);

        Assert.AreEqual(step, listing.BidStep);
        Assert.AreEqual(step, listing.RoundUpToStep(1));
        Assert.AreEqual(step * 3, listing.RoundUpToStep(step * 2 + 1));
    }
}
