using NUnit.Framework;

public class LocalPlayerSpawnWatchTests
{
    [Test]
    public void IsOverdue_OnlyAfterMissingLongerThanWait()
    {
        var watch = new LocalPlayerSpawnWatch(15f);

        Assert.IsFalse(watch.IsOverdue(false, 100f));
        Assert.IsFalse(watch.IsOverdue(false, 115f));
        Assert.IsTrue(watch.IsOverdue(false, 115.1f));
    }

    [Test]
    public void IsOverdue_RestartsCountWhenPlayerAppears()
    {
        var watch = new LocalPlayerSpawnWatch(15f);

        watch.IsOverdue(false, 100f);
        Assert.IsFalse(watch.IsOverdue(true, 110f));
        Assert.IsFalse(watch.IsOverdue(false, 120f));
        Assert.IsTrue(watch.IsOverdue(false, 135.1f));
    }

    [Test]
    public void Reset_RestartsCount()
    {
        var watch = new LocalPlayerSpawnWatch(15f);

        watch.IsOverdue(false, 100f);
        watch.Reset();
        Assert.IsFalse(watch.IsOverdue(false, 120f));
        Assert.IsTrue(watch.IsOverdue(false, 135.1f));
    }
}
