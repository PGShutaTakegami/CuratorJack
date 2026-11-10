/// <summary>アイテムを 1 回使ったこと（誰が・何を・誰に・どの出品の間に）</summary>
public readonly struct ItemUse
{
    public ItemUse(int player, ItemDefinition item, int target, Listing listing)
    {
        Player = player;
        Item = item;
        Target = target;
        Listing = listing;
    }

    public int Player { get; }

    public ItemDefinition Item { get; }

    /// <summary>選んだ相手（相手を選ばないアイテムは PlayerRegistry.NoPlayer）</summary>
    public int Target { get; }

    /// <summary>使ったときのオークションの出品</summary>
    public Listing Listing { get; }
}
