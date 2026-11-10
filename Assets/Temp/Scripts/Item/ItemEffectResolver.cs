using System;

/// <summary>
/// アイテムを使ったときの効果を、全員の画面で同じように反映する（見せ方は Used を受けた表示側が行う）。
/// ・虫眼鏡：状態は変えない（使った人の画面で出品に付いている効果カードを見せる）。
/// ・電気プラグ：使った人にシールドを 1 枚貼る。
/// ・リモコン：選んだ相手（自分以外の生きている人）をマヒさせ、次の出品ターンを飛ばす。相手が正しくなければ使わない。
/// 操作の値には、マスの番号と相手の番号をまとめて入れる（Encode / Decode）。
/// </summary>
public sealed class ItemEffectResolver
{
    readonly GameManager game;

    public ItemEffectResolver(GameManager game)
    {
        this.game = game;
    }

    /// <summary>アイテムを使い、効果を反映した</summary>
    public event Action<ItemUse> Used;

    /// <summary>マスの番号と相手のプレイヤー番号を、操作の値 1 つにまとめる</summary>
    public static int Encode(int slot, int target) => target * ItemInventory.Capacity + slot;

    public static void Decode(int value, out int slot, out int target)
    {
        slot = value % ItemInventory.Capacity;
        target = value / ItemInventory.Capacity;
    }

    /// <summary>オークション中に、player が value（Encode した値）のアイテムを使う。使えなければ false</summary>
    public bool TryUse(int player, int value, Listing listing)
    {
        if (value < 0 || !game.TryGet(player, out PlayerState state) || !state.IsAlive)
            return false;

        Decode(value, out int slot, out int target);
        ItemDefinition item = state.Items.Slots[slot];
        if (item == null)
            return false;
        if (item.NeedsTarget && !IsValidTarget(player, target))
            return false;
        if (!game.TryUseItem(player, slot))
            return false;

        switch (item.Kind)
        {
            case ItemKind.PowerPlug:
                game.AddStatus(player, PlayerStatus.Shield, 1);
                break;
            case ItemKind.RemoteControl:
                game.AddStatus(target, PlayerStatus.Paralysis, 1);
                break;
        }

        Used?.Invoke(new ItemUse(player, item, item.NeedsTarget ? target : PlayerRegistry.NoPlayer, listing));
        return true;
    }

    /// <summary>リモコンの相手にできるか（自分以外の生きている人）</summary>
    public bool IsValidTarget(int player, int target) => target != player && game.IsAlive(target);
}
