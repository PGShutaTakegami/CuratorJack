using NUnit.Framework;

/// <summary>接続するときに送るデータ（バージョン・プレイヤー ID）の組み立てと読み取り、バージョンの照合を確かめる</summary>
public class ConnectionPayloadTests
{
    [Test]
    public void 組み立てたものを読み戻せる()
    {
        ConnectionPayload payload = ConnectionPayload.Parse(new ConnectionPayload("1.2.3", "player-a").ToBytes());

        Assert.AreEqual("1.2.3", payload.Version);
        Assert.AreEqual("player-a", payload.PlayerId);
    }

    [Test]
    public void プレイヤーIDが空でも読み戻せる()
    {
        ConnectionPayload payload = ConnectionPayload.Parse(new ConnectionPayload("1.2.3", null).ToBytes());

        Assert.AreEqual("1.2.3", payload.Version);
        Assert.AreEqual(string.Empty, payload.PlayerId);
    }

    [Test]
    public void 区切りの無い古い形式や空のデータはバージョンが空になる()
    {
        Assert.AreEqual(string.Empty, ConnectionPayload.Parse(System.Text.Encoding.UTF8.GetBytes("player-a")).Version);
        Assert.AreEqual(string.Empty, ConnectionPayload.Parse(null).Version);
        Assert.AreEqual(string.Empty, ConnectionPayload.Parse(new byte[0]).Version);
    }

    [Test]
    public void 大きすぎるデータは読まない()
    {
        Assert.AreEqual(string.Empty, ConnectionPayload.Parse(new ConnectionPayload("1.2.3", new string('a', 300)).ToBytes()).Version);
    }

    [Test]
    public void 同じバージョンだけを遊べるものとみなす()
    {
        Assert.IsTrue(GameVersion.IsCompatible(GameVersion.Current));
        Assert.IsFalse(GameVersion.IsCompatible(GameVersion.Current + ".1"));
        Assert.IsFalse(GameVersion.IsCompatible(string.Empty));
        Assert.IsFalse(GameVersion.IsCompatible(null));
    }
}
