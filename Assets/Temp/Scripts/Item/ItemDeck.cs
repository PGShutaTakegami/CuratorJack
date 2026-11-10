using System.Collections.Generic;

/// <summary>
/// 落札した人に配るアイテムをランダムに引く。
/// ネットワークではシードをそろえ、全員の画面で同じ順に引いて同じアイテムにする。
/// </summary>
public sealed class ItemDeck
{
    readonly IReadOnlyList<ItemDefinition> items;
    System.Random random = new System.Random();

    public ItemDeck(IReadOnlyList<ItemDefinition> items)
    {
        this.items = items;
    }

    public void Reseed(int seed)
    {
        random = new System.Random(seed);
    }

    /// <summary>候補が無ければ null</summary>
    public ItemDefinition Draw()
    {
        return items == null || items.Count == 0 ? null : items[random.Next(items.Count)];
    }
}
