using NUnit.Framework;

/// <summary>戦績を保存した文字列から戻せること・壊れた文字列なら空の戦績になることを確かめる</summary>
public class PlayerRecordTests
{
    [Test]
    public void 保存した戦績を文字列から元に戻せる()
    {
        var record = new PlayerRecord();
        record.AddGamePlayed();
        record.AddGamePlayed();
        record.AddWin();
        record.AddPurchase();
        record.AddPurchase();
        record.AddPurchase();
        record.AddSale();
        record.AddItemUsed();

        PlayerRecord loaded = PlayerRecord.FromJson(record.ToJson());

        Assert.AreEqual(2, loaded.GamesPlayed);
        Assert.AreEqual(1, loaded.Wins);
        Assert.AreEqual(3, loaded.Purchases);
        Assert.AreEqual(1, loaded.Sales);
        Assert.AreEqual(1, loaded.ItemsUsed);
    }

    [TestCase("")]
    [TestCase(null)]
    [TestCase("{壊れた")]
    public void 空や壊れた文字列なら何も記録していない戦績になる(string json)
    {
        PlayerRecord loaded = PlayerRecord.FromJson(json);

        Assert.AreEqual(0, loaded.GamesPlayed);
        Assert.AreEqual(0, loaded.Wins);
        Assert.AreEqual(0, loaded.Purchases);
        Assert.AreEqual(0, loaded.Sales);
        Assert.AreEqual(0, loaded.ItemsUsed);
    }
}
