using NUnit.Framework;

/// <summary>RPC の回数制限（まとめて受け付ける回数・時間での回復・上限）を確かめる</summary>
public class RpcRateLimiterTests
{
    [Test]
    public void まとめて受け付けるのは上限の回数まで()
    {
        var limiter = new RpcRateLimiter(3f, 1f);

        Assert.IsTrue(limiter.TryConsume(0f));
        Assert.IsTrue(limiter.TryConsume(0f));
        Assert.IsTrue(limiter.TryConsume(0f));
        Assert.IsFalse(limiter.TryConsume(0f));
    }

    [Test]
    public void 時間がたつと回復する()
    {
        var limiter = new RpcRateLimiter(1f, 2f);
        limiter.TryConsume(0f);

        Assert.IsFalse(limiter.TryConsume(0.25f));
        Assert.IsTrue(limiter.TryConsume(0.5f));
    }

    [Test]
    public void 長く空いても上限を超えて溜まらない()
    {
        var limiter = new RpcRateLimiter(2f, 10f);
        limiter.TryConsume(0f);

        Assert.IsTrue(limiter.TryConsume(100f));
        Assert.IsTrue(limiter.TryConsume(100f));
        Assert.IsFalse(limiter.TryConsume(100f));
    }

    [Test]
    public void 時刻が戻っても回復しない()
    {
        var limiter = new RpcRateLimiter(1f, 1f);
        limiter.TryConsume(10f);

        Assert.IsFalse(limiter.TryConsume(5f));
        Assert.IsFalse(limiter.TryConsume(10.5f));
        Assert.IsTrue(limiter.TryConsume(11f));
    }
}
