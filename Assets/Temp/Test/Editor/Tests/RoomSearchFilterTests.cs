using NUnit.Framework;

/// <summary>部屋一覧の絞り込み（ルーム名・人数・パスワードの有無）とパスワードの入力規則を確かめる</summary>
public class RoomSearchFilterTests
{
    static RoomInfo Room(string name, int capacity, bool hasPassword) =>
        new RoomInfo { roomName = name, capacity = capacity, hasPassword = hasPassword };

    [Test]
    public void 条件なしなら全部通す()
    {
        Assert.IsTrue(RoomSearchFilter.None.Matches(Room("A", 2, false)));
        Assert.IsTrue(RoomSearchFilter.None.Matches(Room("B", 4, true)));
    }

    [Test]
    public void ルーム名は大文字小文字を区別せず部分一致で探す()
    {
        var filter = new RoomSearchFilter(" jack ", 0, RoomPasswordCondition.Any);

        Assert.IsTrue(filter.Matches(Room("CuratorJack部屋", 4, false)));
        Assert.IsFalse(filter.Matches(Room("4人部屋 #1234", 4, false)));
    }

    [Test]
    public void 人数を指定するとその人数の部屋だけ通す()
    {
        var filter = RoomSearchFilter.None.WithCapacity(3);

        Assert.IsTrue(filter.Matches(Room("A", 3, false)));
        Assert.IsFalse(filter.Matches(Room("A", 4, false)));
    }

    [Test]
    public void パスワードの有無で絞り込む()
    {
        var with = new RoomSearchFilter(string.Empty, 0, RoomPasswordCondition.With);
        var without = new RoomSearchFilter(string.Empty, 0, RoomPasswordCondition.Without);

        Assert.IsTrue(with.Matches(Room("A", 2, true)));
        Assert.IsFalse(with.Matches(Room("A", 2, false)));
        Assert.IsTrue(without.Matches(Room("A", 2, false)));
        Assert.IsFalse(without.Matches(Room("A", 2, true)));
    }

    [Test]
    public void 人数を変えても他の条件は残る()
    {
        var filter = new RoomSearchFilter("abc", 2, RoomPasswordCondition.With).WithCapacity(4);

        Assert.AreEqual("abc", filter.NameKeyword);
        Assert.AreEqual(4, filter.Capacity);
        Assert.AreEqual(RoomPasswordCondition.With, filter.Password);
    }

    [Test]
    public void 絞り込みは元の並び順を保つ()
    {
        var rooms = new[] { Room("a1", 2, false), Room("b", 2, false), Room("a2", 2, false) };

        var matched = new RoomSearchFilter("a", 0, RoomPasswordCondition.Any).Apply(rooms);

        Assert.AreEqual(2, matched.Count);
        Assert.AreEqual("a1", matched[0].roomName);
        Assert.AreEqual("a2", matched[1].roomName);
    }

    [Test]
    public void パスワードは半角英数字1から16文字()
    {
        Assert.IsTrue(RoomPasswordRule.IsValid("a"));
        Assert.IsTrue(RoomPasswordRule.IsValid("Abc123Abc123Abc1"));
        Assert.IsFalse(RoomPasswordRule.IsValid(string.Empty));
        Assert.IsFalse(RoomPasswordRule.IsValid("Abc123Abc123Abc12"));
        Assert.IsFalse(RoomPasswordRule.IsValid("ab c"));
        Assert.IsFalse(RoomPasswordRule.IsValid("あいう"));
    }

    [Test]
    public void Lobbyに渡すパスワードは8文字以上になる()
    {
        Assert.GreaterOrEqual(OnlineRoomPassword.ToSessionPassword("1").Length, 8);
        Assert.LessOrEqual(OnlineRoomPassword.ToSessionPassword(new string('a', RoomPasswordRule.MaxLength)).Length, 64);
    }
}
