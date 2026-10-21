using NUnit.Framework;

/// <summary>カード効果を、仮想の対戦卓で受け取ったことにして結果を確かめる</summary>
public class CardEffectResolverTests
{
    const int Seller = 1;
    const int Buyer = 2;

    static CardEffectResolver Resolver(VirtualGame table)
    {
        var resolver = new CardEffectResolver(table.Game, player => $"P{player}", _ => null);
        resolver.Reseed(1234);
        return resolver;
    }

    static EffectReport Receive(VirtualGame table, int recipient, params CardEffect[] buffs)
    {
        HandCard[] cards = System.Array.ConvertAll(buffs, TestCards.Effect);
        return Resolver(table).ResolveReceived(recipient, Seller, cards, null);
    }

    static EffectReport ReceiveSpecial(VirtualGame table, int recipient, CardEffect special)
    {
        return Resolver(table).ResolveReceived(recipient, Seller, new HandCard[0], TestCards.WithEffect(special));
    }

    [Test]
    public void 黄金律でコインが20枚増える()
    {
        using var table = new VirtualGame(3, coins: 50);

        EffectReport report = ReceiveSpecial(table, Buyer, CardEffect.GoldenBoon);

        Assert.AreEqual(70, table.Player(Buyer).Coins);
        Assert.AreEqual(1, report.CoinChanges.Count);
    }

    [Test]
    public void 猶予の証で見送り権が1枚増える()
    {
        using var table = new VirtualGame(3, passRights: 3);

        ReceiveSpecial(table, Buyer, CardEffect.GraceToken);

        Assert.AreEqual(4, table.Player(Buyer).PassRights);
    }

    [Test]
    public void 免罪符があると受けた効果はすべて無効になり1回使う()
    {
        using var table = new VirtualGame(3);
        table.Game.AddStatus(Buyer, PlayerStatus.Indulgence, 1);

        Receive(table, Buyer, CardEffect.Pressure, CardEffect.Interference);

        Assert.AreEqual(0, table.Player(Buyer).StatusOf(PlayerStatus.Pressure));
        Assert.AreEqual(0, table.Player(Buyer).StatusOf(PlayerStatus.Interference));
        Assert.AreEqual(0, table.Player(Buyer).StatusOf(PlayerStatus.Indulgence));
    }

    [Test]
    public void 値踏みで購入者が出品者へ1から3コイン払う()
    {
        using var table = new VirtualGame(3, coins: 100);

        Receive(table, Buyer, CardEffect.Markdown);

        int paid = 100 - table.Player(Buyer).Coins;
        Assert.That(paid, Is.InRange(1, 3));
        Assert.AreEqual(100 + paid, table.Player(Seller).Coins);
    }

    [Test]
    public void 値踏みは出品者が受け取ったら何も起きない()
    {
        using var table = new VirtualGame(3, coins: 100);

        Receive(table, Seller, CardEffect.Markdown);

        Assert.AreEqual(100, table.Player(Seller).Coins);
    }

    [Test]
    public void 全横やりは受け取った人以外にかかり免罪符の人は防ぐ()
    {
        using var table = new VirtualGame(4);
        table.Game.AddStatus(3, PlayerStatus.Indulgence, 1);

        ReceiveSpecial(table, Buyer, CardEffect.InterferenceAll);

        Assert.AreEqual(1, table.Player(Seller).StatusOf(PlayerStatus.Interference));
        Assert.AreEqual(0, table.Player(Buyer).StatusOf(PlayerStatus.Interference));
        Assert.AreEqual(0, table.Player(3).StatusOf(PlayerStatus.Interference));
        Assert.AreEqual(0, table.Player(3).StatusOf(PlayerStatus.Indulgence));
        Assert.AreEqual(1, table.Player(4).StatusOf(PlayerStatus.Interference));
    }

    [Test]
    public void 逆転と2度目と投げ売りの結果()
    {
        using var table = new VirtualGame(3);

        EffectReport report = Receive(table, Buyer, CardEffect.Reversal, CardEffect.SecondTurn, CardEffect.FireSale);

        Assert.AreEqual(1, report.Reversals);
        Assert.AreEqual(1, table.Player(Buyer).StatusOf(PlayerStatus.ExtraTurn));
        Assert.That(table.Player(Buyer).StatusOf(PlayerStatus.FireSaleDiscount), Is.InRange(1, 5));
    }

    [Test]
    public void 口コミと伝手は追加のドローになる()
    {
        using var table = new VirtualGame(3);

        EffectReport report = Receive(table, Buyer, CardEffect.WordOfMouth, CardEffect.Connections);

        Assert.AreEqual(2, report.Draws.Count);
        Assert.AreEqual(CardManager.CardKinds.EffectCard, report.Draws[0].Kind);
        Assert.AreEqual(CardManager.CardKinds.GoodCard, report.Draws[1].Kind);
    }

    [Test]
    public void ランダムは受け取った人以外に全体効果のどれかを発動する()
    {
        using var table = new VirtualGame(3);

        EffectReport report = ReceiveSpecial(table, Buyer, CardEffect.Random);

        bool OthersHave(PlayerStatus status) =>
            table.Player(Seller).StatusOf(status) == 1 && table.Player(3).StatusOf(status) == 1;
        bool appraisedAll = report.Messages.Count > 0 && report.Messages[0].Contains("全鑑定");
        Assert.IsTrue(OthersHave(PlayerStatus.Interference) || OthersHave(PlayerStatus.Pressure) || appraisedAll, string.Join("\n", report.Messages));
        Assert.AreEqual(0, table.Player(Buyer).StatusOf(PlayerStatus.Interference) + table.Player(Buyer).StatusOf(PlayerStatus.Pressure));
    }
}
