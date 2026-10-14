using System;
using System.Collections.Generic;

/// <summary>
/// 出品者が出品する商品を決める手続き。
/// 最初にグッズカードを 1 枚選び（選んだ時点で手札から消費）、そのあとバフデバフカードを付けるか選び（押した時点で消費）、確定する。
/// バフデバフカードを付けた場合は、最低出品額を増額するか（YES / NO）も選べる。
/// </summary>
public sealed class ListingSession
{
    readonly GameManager game;
    readonly List<HandCard> buffs = new List<HandCard>();

    public ListingSession(GameManager game, int seller)
    {
        this.game = game;
        Seller = seller;
    }

    public int Seller { get; }

    public HandCard Goods { get; private set; }

    public IReadOnlyList<HandCard> Buffs => buffs;

    public bool IsConfirmed { get; private set; }

    /// <summary>増額を選べるか（バフデバフカードを 1 枚以上付けている）</summary>
    public bool CanRaise => buffs.Count > 0;

    /// <summary>増額する（YES）を選んでいるか。付けていなければ常に false</summary>
    public bool IsRaised { get; private set; }

    /// <summary>今の選択で確定したときの最低出品額</summary>
    public int MinPrice => Goods == null ? 0 : Listing.InitialPriceOf(Goods) + (IsRaised ? Listing.PriceRaise : 0);

    public bool CanConfirm => Goods != null && !IsConfirmed;

    public event Action Changed;

    public bool TrySelectGoods(HandCard card)
    {
        if (IsConfirmed || Goods != null || !card.IsGoods || !game.RemoveFromHand(Seller, card))
            return false;

        Goods = card;
        Changed?.Invoke();
        return true;
    }

    public bool TryAttachBuff(HandCard card)
    {
        if (IsConfirmed || Goods == null || card.IsGoods || !game.RemoveFromHand(Seller, card))
            return false;

        buffs.Add(card);
        Changed?.Invoke();
        return true;
    }

    public bool TrySetRaise(bool raise)
    {
        if (IsConfirmed || !CanRaise)
            return false;

        IsRaised = raise;
        Changed?.Invoke();
        return true;
    }

    public bool TryConfirm()
    {
        if (!CanConfirm)
            return false;

        IsConfirmed = true;
        Changed?.Invoke();
        return true;
    }

    public Listing ToListing()
    {
        return new Listing(Seller, Goods, buffs.ToArray(), IsRaised);
    }
}
