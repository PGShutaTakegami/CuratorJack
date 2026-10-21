using System.Collections.Generic;

/// <summary>
/// 出品された商品（グッズカード 1 枚と、出品者が付けたバフデバフカード）。
/// 最低出品額は商品の初期価格（増額していれば +PriceRaise、投げ売りがかかっていれば -値引き額、0 未満にはならない）、
/// 入札額を変える幅は商品のレアリティで決まる。
/// </summary>
public sealed class Listing
{
    /// <summary>バフデバフカードを付けたときに選べる増額（何枚付けてもこの額まで）</summary>
    public const int PriceRaise = 5;

    public Listing(int seller, HandCard goods, IReadOnlyList<HandCard> buffs, bool isRaised, int discount = 0)
    {
        Seller = seller;
        Goods = goods;
        Buffs = buffs;
        IsRaised = isRaised && buffs.Count > 0;
        Discount = discount;
    }

    public bool IsRaised { get; }

    public int Seller { get; }

    public HandCard Goods { get; }

    public IReadOnlyList<HandCard> Buffs { get; }

    /// <summary>増額する前の最低出品額（商品の初期価格）</summary>
    public int BasePrice => InitialPriceOf(Goods);

    /// <summary>最低出品額に足した増額（増額していなければ 0）</summary>
    public int Raise => IsRaised ? PriceRaise : 0;

    /// <summary>投げ売りで最低出品額から引いた額（かかっていなければ 0）</summary>
    public int Discount { get; }

    public int MinPrice => PriceOf(BasePrice, Raise, Discount);

    public static int InitialPriceOf(HandCard goods) => goods.Data != null ? goods.Data.Parameter.InitialPrice : 0;

    public static int PriceOf(int basePrice, int raise, int discount) => System.Math.Max(0, basePrice + raise - discount);

    public int BidStep => StepOf(Goods.Data != null ? Goods.Data.Rarity : GoodsRarity.Low);

    /// <summary>amount 以上で、入札額を 0 から BidStep ずつ変えて選べる最小の額</summary>
    public int RoundUpToStep(int amount) => (amount + BidStep - 1) / BidStep * BidStep;

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
