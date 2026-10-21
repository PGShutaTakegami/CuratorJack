using NUnit.Framework;

/// <summary>ネットワークのプレイヤー番号の割り当て（部屋の人数・CPU・空き番号の再利用）を確かめる</summary>
public class PlayerSlotAllocatorTests
{
    [Test]
    public void 人数を超えては割り当てない()
    {
        var allocator = new PlayerSlotAllocator(3, 4);

        allocator.TryAssign(10, out int first);
        allocator.TryAssign(11, out int second);
        allocator.TryAssign(12, out int third);

        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, new[] { first, second, third });
        Assert.IsFalse(allocator.TryAssign(13, out _));
    }

    [Test]
    public void 抜けた番号は次の人に使う()
    {
        var allocator = new PlayerSlotAllocator(3, 4);
        allocator.TryAssign(10, out _);
        allocator.TryAssign(11, out _);

        allocator.TryRelease(10, out int released);
        allocator.TryAssign(12, out int reused);

        Assert.AreEqual(1, released);
        Assert.AreEqual(1, reused);
    }

    [Test]
    public void 人数は席の数を超えない()
    {
        var allocator = new PlayerSlotAllocator(3, 4);

        allocator.SetMaxPlayers(9);

        Assert.AreEqual(4, allocator.MaxPlayers);
    }

    [Test]
    public void CPUも人数に数える()
    {
        var allocator = new PlayerSlotAllocator(3, 4);
        allocator.TryAssign(0, out int host);
        allocator.TryAssignCpu(out int cpu);
        allocator.TryAssign(5, out int guest);

        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, new[] { host, cpu, guest });
        Assert.IsTrue(allocator.IsCpu(cpu));
        Assert.AreEqual(3, allocator.Count);
        Assert.IsFalse(allocator.TryAssignCpu(out _));
        Assert.IsFalse(allocator.TryAssign(6, out _));
    }

    [Test]
    public void CPUを外した番号は人に使える()
    {
        var allocator = new PlayerSlotAllocator(2, 4);
        allocator.TryAssign(0, out _);
        allocator.TryAssignCpu(out int cpu);

        allocator.ReleaseCpu(cpu);
        allocator.TryAssign(7, out int guest);

        Assert.AreEqual(cpu, guest);
        Assert.IsTrue(allocator.TryGetClient(guest, out ulong client));
        Assert.AreEqual(7ul, client);
    }
}
