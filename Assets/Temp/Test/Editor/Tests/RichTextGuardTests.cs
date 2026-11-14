using NUnit.Framework;

/// <summary>他のプレイヤーが決めた文字から、TextMeshPro のタグになる文字を取り除けるかを確かめる</summary>
public class RichTextGuardTests
{
    [Test]
    public void タグになる文字を取り除く()
    {
        Assert.AreEqual("size=500%巨大", RichTextGuard.Strip("<size=500%>巨大"));
    }

    [Test]
    public void タグのない文字はそのまま()
    {
        Assert.AreEqual("キュレーター", RichTextGuard.Strip("キュレーター"));
    }

    [Test]
    public void 空やnullは空にする()
    {
        Assert.AreEqual(string.Empty, RichTextGuard.Strip(null));
        Assert.AreEqual(string.Empty, RichTextGuard.Strip(string.Empty));
    }

    [Test]
    public void 名前の整形でもタグになる文字を取り除く()
    {
        Assert.AreEqual("bJack/b", PlayerNameContext.Sanitize(" <b>Jack</b> "));
    }
}
