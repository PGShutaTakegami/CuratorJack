using System.Collections.Generic;
using NUnit.Framework;

/// <summary>出品者の順番（時計回り・逆転・脱落者の飛ばし・1 周の判定）を確かめる</summary>
public class TurnOrderTests
{
    static readonly int[] Clockwise = { 1, 2, 3, 4 };

    [Test]
    public void 時計回りで脱落した人を飛ばす()
    {
        var order = new TurnOrder(Clockwise);
        var dead = new HashSet<int> { 2 };

        Assert.AreEqual(3, order.NextAlive(1, player => !dead.Contains(player)));
        Assert.AreEqual(1, order.NextAlive(4, player => !dead.Contains(player)));
    }

    [Test]
    public void 逆転すると反対回りになる()
    {
        var order = new TurnOrder(Clockwise);

        order.Reverse();

        Assert.IsTrue(order.IsReversed);
        Assert.AreEqual(4, order.NextAlive(1, _ => true));
    }

    [Test]
    public void 生きている全員が出品したら1周()
    {
        var order = new TurnOrder(Clockwise);
        var dead = new HashSet<int> { 3 };
        bool IsAlive(int player) => !dead.Contains(player);

        order.MarkTurnDone(1);
        order.MarkTurnDone(2);
        Assert.IsFalse(order.IsRoundComplete(IsAlive));

        order.MarkTurnDone(4);
        Assert.IsTrue(order.IsRoundComplete(IsAlive));

        order.StartNewRound();
        Assert.IsFalse(order.IsRoundComplete(IsAlive));
    }

    [Test]
    public void 誰も生きていなければ次はいない()
    {
        var order = new TurnOrder(Clockwise);

        Assert.AreEqual(PlayerRegistry.NoPlayer, order.NextAlive(1, _ => false));
    }
}
