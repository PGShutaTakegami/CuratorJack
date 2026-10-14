using System.Collections.Generic;

/// <summary>
/// 出品された商品（グッズカード 1 枚と、出品者が付けたバフデバフカード）。
/// 最低出品額は商品の初期価格（増額していれば +PriceRaise）、入札額を変える幅は商品のレアリティで決まる。
/// </summary>
public sealed class Listing
{
    /// <summary>バフデバフカードを付けたときに選べる増額（何枚付けてもこの額まで）</summary>
    public const int PriceRaise = 5;

    public Listing(int seller, HandCard goods, IReadOnlyList<HandCard> buffs, bool isRaised)
    {
        Seller = seller;
        Goods = goods;
        Buffs = buffs;
        IsRaised = isRaised && buffs.Count > 0;
    }

    public bool IsRaised { get; }

    public int Seller { get; }

    public HandCard Goods { get; }

    public IReadOnlyList<HandCard> Buffs { get; }

    public int MinPrice => InitialPriceOf(Goods) + (IsRaised ? PriceRaise : 0);

    public static int InitialPriceOf(HandCard goods) => goods.Data != null ? goods.Data.Parameter.InitialPrice : 0;

    public int BidStep => StepOf(Goods.Data != null ? Goods.Data.Rarity : GoodsRarity.Low);

    /// <summary>入札額を 1 回で変える幅（低品質 1、中品質 2、最高品質 5）</summary>
    public static int StepOf(GoodsRarity rarity)
    {
        switch (rarity)
        {
            case GoodsRarity.Highest: return 5;
            case GoodsRarity.Middle: return 2;
            default: return 1;
        }
    }
}
