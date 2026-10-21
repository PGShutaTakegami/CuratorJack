using NUnit.Framework;

/// <summary>見送り権による脱落と、購入の代金の流れを仮想の対戦卓で確かめる</summary>
public class GameManagerTests
{
    [Test]
    public void 見送り権が足りないと使い切って脱落する()
    {
        using var table = new VirtualGame(3, passRights: 3);
        PlayerState died = null;
        table.Game.PlayerDied += player => died = player;

        table.Game.ConsumePassRight(2, 2);
        Assert.IsTrue(table.Player(2).IsAlive);
        Assert.AreEqual(1, table.Player(2).PassRights);

        table.Game.ConsumePassRight(2, 2);
        Assert.IsFalse(table.Player(2).IsAlive);
        Assert.AreEqual(0, table.Player(2).PassRights);
        Assert.AreSame(table.Player(2), died);
    }

    [Test]
    public void 生き残りが3人以上なら代金は出品者に入る()
    {
        using var table = new VirtualGame(3, coins: 100);

        table.Game.SettlePurchase(2, 1, TestCards.BlankGoods(), 30);

        Assert.AreEqual(70, table.Player(2).Coins);
        Assert.AreEqual(130, table.Player(1).Coins);
        Assert.AreEqual(1, table.Player(2).Collection.Count);
    }

    [Test]
    public void 最後の1v1では代金は出品者に入らない()
    {
        using var table = new VirtualGame(3, coins: 100);
        table.Game.Retire(3);

        table.Game.SettlePurchase(2, 1, TestCards.BlankGoods(), 30);

        Assert.AreEqual(70, table.Player(2).Coins);
        Assert.AreEqual(100, table.Player(1).Coins);
    }

    [Test]
    public void 追加の代金は持っている分までしか払わない()
    {
        using var table = new VirtualGame(3, coins: 2);

        int paid = table.Game.PayProceeds(2, 1, 3);

        Assert.AreEqual(2, paid);
        Assert.AreEqual(0, table.Player(2).Coins);
        Assert.AreEqual(4, table.Player(1).Coins);
    }

    [Test]
    public void リタイアすると見送り権を手放して脱落する()
    {
        using var table = new VirtualGame(2);

        table.Game.Retire(2);

        Assert.IsFalse(table.Player(2).IsAlive);
        Assert.AreEqual(0, table.Player(2).PassRights);
        CollectionAssert.AreEqual(new[] { 1 }, table.Alive);
    }
}
