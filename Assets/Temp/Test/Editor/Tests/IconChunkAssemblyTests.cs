using NUnit.Framework;

/// <summary>分けて送られてきたアイコンの PNG のまとめ方（順番・送り直し・上限）を確かめる</summary>
public class IconChunkAssemblyTests
{
    [Test]
    public void 全部そろうと順番どおりにまとめる()
    {
        var assembly = new IconChunkAssembly(100, 10);

        Assert.IsNull(assembly.Add(1, 1, 2, new byte[] { 3, 4 }));
        byte[] png = assembly.Add(1, 0, 2, new byte[] { 1, 2 });

        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, png);
    }

    [Test]
    public void 新しい送信が始まると途中のものは捨てる()
    {
        var assembly = new IconChunkAssembly(100, 10);
        assembly.Add(1, 0, 2, new byte[] { 1 });

        Assert.IsNull(assembly.Add(2, 0, 2, new byte[] { 5 }));
        CollectionAssert.AreEqual(new byte[] { 5, 6 }, assembly.Add(2, 1, 2, new byte[] { 6 }));
    }

    [Test]
    public void 大きすぎるものは受け付けない()
    {
        var assembly = new IconChunkAssembly(3, 10);

        Assert.IsNull(assembly.Add(1, 0, 2, new byte[] { 1, 2 }));
        Assert.IsNull(assembly.Add(1, 1, 2, new byte[] { 3, 4 }));
    }

    [Test]
    public void 断片の数が多すぎるものや番号が範囲外のものは受け付けない()
    {
        var assembly = new IconChunkAssembly(100, 2);

        Assert.IsNull(assembly.Add(1, 0, 3, new byte[] { 1 }));
        Assert.IsNull(assembly.Add(1, 2, 2, new byte[] { 1 }));
        CollectionAssert.AreEqual(new byte[] { 9 }, assembly.Add(1, 0, 1, new byte[] { 9 }));
    }
}
