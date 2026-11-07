using NUnit.Framework;

/// <summary>キック済みの一覧（登録・ホストの引き継ぎ用の受け渡し）を確かめる</summary>
public class KickedPlayerListTests
{
    [Test]
    public void 登録したIDだけが含まれる()
    {
        var list = new KickedPlayerList();
        list.Add("player-a");

        Assert.IsTrue(list.Contains("player-a"));
        Assert.IsFalse(list.Contains("player-b"));
        Assert.IsFalse(list.Contains(string.Empty));
        Assert.IsFalse(list.Contains(null));
    }

    [Test]
    public void バイト列にして受け渡せる()
    {
        var source = new KickedPlayerList();
        source.Add("player-a");
        source.Add("player-b");

        var target = new KickedPlayerList();
        target.Load(source.Serialize());

        Assert.IsTrue(target.Contains("player-a"));
        Assert.IsTrue(target.Contains("player-b"));
    }

    [Test]
    public void 空や意味の無い1バイトを読むと空の一覧になる()
    {
        var list = new KickedPlayerList();
        list.Add("player-a");

        list.Load(new byte[1]);
        Assert.IsFalse(list.Contains("player-a"));

        list.Load(null);
        Assert.IsFalse(list.Contains("player-a"));
    }
}
