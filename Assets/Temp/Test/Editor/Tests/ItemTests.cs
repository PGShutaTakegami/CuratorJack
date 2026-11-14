using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>アイテムのマス・ランダムに引く山・オークション中だけ使えること・種類ごとの効果を確かめる</summary>
public class ItemTests
{
    const int Seller = 1;
    const int NoTarget = PlayerRegistry.NoPlayer;

    readonly List<ItemDefinition> created = new List<ItemDefinition>();

    [TearDown]
    public void DestroyItems()
    {
        foreach (ItemDefinition item in created)
            Object.DestroyImmediate(item);
        created.Clear();
    }

    ItemDefinition NewItem(ItemKind kind = ItemKind.Magnifier)
    {
        var item = ScriptableObject.CreateInstance<ItemDefinition>();
        var serialized = new SerializedObject(item);
        serialized.FindProperty("kind").enumValueIndex = (int)kind;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        created.Add(item);
        return item;
    }

    static PlayerActionDispatcher StartAuction(VirtualGame table)
    {
        var dispatcher = new PlayerActionDispatcher(table.Game);
        var listing = new Listing(Seller, TestCards.BlankGoods(), new HandCard[0], false);
        dispatcher.BeginAuction(new AuctionModel(listing, new[] { 2, 3 }, player => table.Player(player).Coins, 2));
        return dispatcher;
    }

    [Test]
    public void マスは3つまでで満杯ならもらえない()
    {
        using var table = new VirtualGame(3);

        for (int i = 0; i < ItemInventory.Capacity; i++)
            Assert.IsTrue(table.Game.TryGiveItem(2, NewItem()));

        Assert.IsTrue(table.Player(2).Items.IsFull);
        Assert.IsFalse(table.Game.TryGiveItem(2, NewItem()));
    }

    [Test]
    public void 使ったマスは詰めずに空き新しいアイテムは左の空きに入る()
    {
        using var table = new VirtualGame(3);
        ItemDefinition first = NewItem();
        ItemDefinition second = NewItem();
        ItemDefinition third = NewItem();
        table.Game.TryGiveItem(2, first);
        table.Game.TryGiveItem(2, second);
        table.Game.TryGiveItem(2, third);

        Assert.IsTrue(table.Game.TryUseItem(2, 0));
        Assert.IsNull(table.Player(2).Items.Slots[0]);
        Assert.AreSame(second, table.Player(2).Items.Slots[1]);
        Assert.IsFalse(table.Game.TryUseItem(2, 0));

        ItemDefinition next = NewItem();
        table.Game.TryGiveItem(2, next);
        Assert.AreSame(next, table.Player(2).Items.Slots[0]);
    }

    [Test]
    public void 同じシードなら同じ順にアイテムを引く()
    {
        var items = new List<ItemDefinition> { NewItem(), NewItem(), NewItem() };
        var a = new ItemDeck(items);
        var b = new ItemDeck(items);
        a.Reseed(1234);
        b.Reseed(1234);

        for (int i = 0; i < 10; i++)
            Assert.AreSame(a.Draw(), b.Draw());
    }

    [Test]
    public void オークション中は出品者も観戦者もアイテムを使える()
    {
        using var table = new VirtualGame(4);
        table.Game.TryGiveItem(Seller, NewItem(ItemKind.PowerPlug));
        table.Game.TryGiveItem(4, NewItem());
        PlayerActionDispatcher dispatcher = StartAuction(table);

        dispatcher.UseItem(Seller, 0, Seller);
        dispatcher.UseItem(4, 0, NoTarget);

        Assert.IsNull(table.Player(Seller).Items.Slots[0]);
        Assert.IsNull(table.Player(4).Items.Slots[0]);
    }

    [Test]
    public void 出品者は自分の出品のオークションで虫眼鏡を使えない()
    {
        using var table = new VirtualGame(3);
        ItemDefinition magnifier = NewItem(ItemKind.Magnifier);
        table.Game.TryGiveItem(Seller, magnifier);
        PlayerActionDispatcher dispatcher = StartAuction(table);

        dispatcher.UseItem(Seller, 0, NoTarget);

        Assert.AreSame(magnifier, table.Player(Seller).Items.Slots[0]);
    }

    [Test]
    public void 出品中や手続きの外ではアイテムを使えない()
    {
        using var table = new VirtualGame(3);
        ItemDefinition item = NewItem();
        table.Game.TryGiveItem(2, item);
        var dispatcher = new PlayerActionDispatcher(table.Game);

        dispatcher.UseItem(2, 0, NoTarget);
        dispatcher.BeginListing(new ListingSession(table.Game, Seller));
        dispatcher.UseItem(2, 0, NoTarget);

        Assert.AreSame(item, table.Player(2).Items.Slots[0]);
    }

    [Test]
    public void 脱落した人はアイテムを使えない()
    {
        using var table = new VirtualGame(3);
        ItemDefinition item = NewItem();
        table.Game.TryGiveItem(3, item);
        table.Game.Retire(3);

        Assert.IsFalse(table.Game.TryUseItem(3, 0));
        Assert.AreSame(item, table.Player(3).Items.Slots[0]);
    }

    [Test]
    public void マスと相手は操作の値から元に戻せる()
    {
        for (int target = 0; target <= 8; target++)
        {
            for (int slot = 0; slot < ItemInventory.Capacity; slot++)
            {
                ItemEffectResolver.Decode(ItemEffectResolver.Encode(slot, target), out int decodedSlot, out int decodedTarget);
                Assert.AreEqual(slot, decodedSlot);
                Assert.AreEqual(target, decodedTarget);
            }
        }
    }

    [Test]
    public void 電気プラグのシールドは見送り権の代わりに使われる()
    {
        using var table = new VirtualGame(3);
        table.Game.TryGiveItem(2, NewItem(ItemKind.PowerPlug));
        PlayerActionDispatcher dispatcher = StartAuction(table);
        int passRights = table.Player(2).PassRights;

        dispatcher.UseItem(2, 0, 2);
        Assert.AreEqual(1, table.Player(2).StatusOf(PlayerStatus.Shield));

        table.Game.ConsumePassRight(2);
        Assert.AreEqual(0, table.Player(2).StatusOf(PlayerStatus.Shield));
        Assert.AreEqual(passRights, table.Player(2).PassRights);

        table.Game.ConsumePassRight(2);
        Assert.AreEqual(passRights - 1, table.Player(2).PassRights);
    }

    [Test]
    public void 電気プラグは選んだ相手にシールドを貼れる()
    {
        using var table = new VirtualGame(3);
        table.Game.TryGiveItem(2, NewItem(ItemKind.PowerPlug));
        PlayerActionDispatcher dispatcher = StartAuction(table);

        dispatcher.UseItem(2, 0, 3);

        Assert.IsNull(table.Player(2).Items.Slots[0]);
        Assert.AreEqual(1, table.Player(3).StatusOf(PlayerStatus.Shield));
        Assert.AreEqual(0, table.Player(2).StatusOf(PlayerStatus.Shield));
    }

    [Test]
    public void 電気プラグは脱落した人や相手なしでは使わずに残る()
    {
        using var table = new VirtualGame(3);
        ItemDefinition plug = NewItem(ItemKind.PowerPlug);
        table.Game.TryGiveItem(2, plug);
        table.Game.Retire(3);
        PlayerActionDispatcher dispatcher = StartAuction(table);

        dispatcher.UseItem(2, 0, 3);
        dispatcher.UseItem(2, 0, NoTarget);

        Assert.AreSame(plug, table.Player(2).Items.Slots[0]);
        Assert.AreEqual(0, table.Player(3).StatusOf(PlayerStatus.Shield));
    }

    [Test]
    public void シールドは落札が終わると全員消える()
    {
        using var table = new VirtualGame(3);
        table.Game.AddStatus(2, PlayerStatus.Shield, 1);
        table.Game.AddStatus(3, PlayerStatus.Shield, 2);
        int passRights = table.Player(2).PassRights;

        table.Game.ExpireShields();

        Assert.AreEqual(0, table.Player(2).StatusOf(PlayerStatus.Shield));
        Assert.AreEqual(0, table.Player(3).StatusOf(PlayerStatus.Shield));
        Assert.AreEqual(passRights, table.Player(2).PassRights);
    }

    [Test]
    public void シールドで足りない分だけ見送り権を使う()
    {
        using var table = new VirtualGame(3);
        table.Game.AddStatus(2, PlayerStatus.Shield, 1);
        int passRights = table.Player(2).PassRights;

        table.Game.ConsumePassRight(2, 2);

        Assert.AreEqual(0, table.Player(2).StatusOf(PlayerStatus.Shield));
        Assert.AreEqual(passRights - 1, table.Player(2).PassRights);
    }

    [Test]
    public void リモコンで選んだ相手がマヒする()
    {
        using var table = new VirtualGame(3);
        table.Game.TryGiveItem(2, NewItem(ItemKind.RemoteControl));
        PlayerActionDispatcher dispatcher = StartAuction(table);

        dispatcher.UseItem(2, 0, 3);

        Assert.IsNull(table.Player(2).Items.Slots[0]);
        Assert.AreEqual(1, table.Player(3).StatusOf(PlayerStatus.Paralysis));
        Assert.AreEqual(0, table.Player(2).StatusOf(PlayerStatus.Paralysis));
    }

    [Test]
    public void リモコンは自分や脱落した人を相手にできず使わずに残る()
    {
        using var table = new VirtualGame(3);
        ItemDefinition remote = NewItem(ItemKind.RemoteControl);
        table.Game.TryGiveItem(2, remote);
        table.Game.Retire(3);
        PlayerActionDispatcher dispatcher = StartAuction(table);

        dispatcher.UseItem(2, 0, 2);
        dispatcher.UseItem(2, 0, 3);
        dispatcher.UseItem(2, 0, NoTarget);

        Assert.AreSame(remote, table.Player(2).Items.Slots[0]);
        Assert.AreEqual(0, table.Player(2).StatusOf(PlayerStatus.Paralysis));
        Assert.AreEqual(0, table.Player(3).StatusOf(PlayerStatus.Paralysis));
    }
}
