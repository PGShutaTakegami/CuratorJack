using NUnit.Framework;
using UnityEngine;

/// <summary>アイコンのお絵かき（線・全部消す・1 つ戻す・1 つ進む・履歴の上限）を確かめる</summary>
public class IconCanvasTests
{
    static readonly Color32 White = new Color32(255, 255, 255, 255);
    static readonly Color32 Black = new Color32(0, 0, 0, 255);

    static Color32 At(IconCanvas canvas, int x, int y) => canvas.Pixels[y * canvas.Size + x];

    [Test]
    public void 最初は背景の色だけで戻せない()
    {
        var canvas = new IconCanvas(8, White, 30);

        Assert.AreEqual(White, At(canvas, 3, 3));
        Assert.IsFalse(canvas.CanUndo);
        Assert.IsFalse(canvas.CanRedo);
    }

    [Test]
    public void 線を引くと通った所だけ色が付く()
    {
        var canvas = new IconCanvas(16, White, 30);

        canvas.BeginStroke();
        canvas.DrawLine(new Vector2(2.5f, 8.5f), new Vector2(13.5f, 8.5f), 1f, Black);

        Assert.AreEqual(Black, At(canvas, 2, 8));
        Assert.AreEqual(Black, At(canvas, 8, 8));
        Assert.AreEqual(Black, At(canvas, 13, 8));
        Assert.AreEqual(White, At(canvas, 8, 2));
    }

    [Test]
    public void 一つ戻すと線を引く前に戻り一つ進むとまた線が出る()
    {
        var canvas = new IconCanvas(8, White, 30);
        canvas.BeginStroke();
        canvas.DrawLine(new Vector2(4f, 4f), new Vector2(4f, 4f), 1f, Black);

        Assert.IsTrue(canvas.Undo());
        Assert.AreEqual(White, At(canvas, 3, 3));
        Assert.IsTrue(canvas.CanRedo);

        Assert.IsTrue(canvas.Redo());
        Assert.AreEqual(Black, At(canvas, 3, 3));
        Assert.IsFalse(canvas.CanRedo);
    }

    [Test]
    public void 戻したあと新しく描くと進む先は消える()
    {
        var canvas = new IconCanvas(8, White, 30);
        canvas.BeginStroke();
        canvas.DrawLine(new Vector2(4f, 4f), new Vector2(4f, 4f), 1f, Black);
        canvas.Undo();

        canvas.BeginStroke();

        Assert.IsFalse(canvas.CanRedo);
    }

    [Test]
    public void 全部消すと背景に戻り一つ戻すと消す前に戻る()
    {
        var canvas = new IconCanvas(8, White, 30);
        canvas.BeginStroke();
        canvas.DrawLine(new Vector2(4f, 4f), new Vector2(4f, 4f), 1f, Black);

        canvas.Clear();
        Assert.AreEqual(White, At(canvas, 3, 3));

        canvas.Undo();
        Assert.AreEqual(Black, At(canvas, 3, 3));
    }

    [Test]
    public void 戻せる回数は上限まで()
    {
        var canvas = new IconCanvas(8, White, 3);
        for (int i = 0; i < 5; i++)
            canvas.BeginStroke();

        int undone = 0;
        while (canvas.Undo())
            undone++;

        Assert.AreEqual(3, undone);
    }

    [Test]
    public void 読み込むと履歴が消え大きさが違うものは背景になる()
    {
        var canvas = new IconCanvas(2, White, 30);
        canvas.BeginStroke();

        canvas.Load(new[] { Black, Black, Black, Black });
        Assert.AreEqual(Black, At(canvas, 1, 1));
        Assert.IsFalse(canvas.CanUndo);

        canvas.Load(new[] { Black });
        Assert.AreEqual(White, At(canvas, 1, 1));
    }
}
