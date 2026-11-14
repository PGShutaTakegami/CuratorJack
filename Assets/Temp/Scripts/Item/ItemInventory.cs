using System.Collections.Generic;

/// <summary>
/// プレイヤーが持つアイテムのマス（最大 Capacity 個）。
/// 使ってもマスの位置は詰めず、新しいアイテムは左から空いているマスに入れる（送った番号と別のアイテムを使わないため）。
/// </summary>
public sealed class ItemInventory
{
    public const int Capacity = 3;

    readonly ItemDefinition[] slots = new ItemDefinition[Capacity];

    /// <summary>マスの中身（空きは null）</summary>
    public IReadOnlyList<ItemDefinition> Slots => slots;

    public bool IsFull => System.Array.IndexOf(slots, null) < 0;

    /// <summary>空いているマスに入れる。満杯なら入れずに false を返す</summary>
    internal bool TryAdd(ItemDefinition item)
    {
        int empty = System.Array.IndexOf(slots, null);
        if (item == null || empty < 0)
            return false;

        slots[empty] = item;
        return true;
    }

    /// <summary>slot のアイテムを取り出す。空きや範囲外なら false を返す</summary>
    internal bool TryTake(int slot, out ItemDefinition item)
    {
        item = slot >= 0 && slot < Capacity ? slots[slot] : null;
        if (item == null)
            return false;

        slots[slot] = null;
        return true;
    }
}
