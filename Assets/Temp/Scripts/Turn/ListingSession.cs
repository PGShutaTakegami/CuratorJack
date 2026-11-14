using System;
using System.Collections.Generic;

/// <summary>
/// 出品者が出品する商品を決める手続き。
/// 最初にグッズカードを 1 枚選び（選んだ時点で手札から消費）、そのあとバフデバフカードを付けるか選び（押した時点で消費）、確定する。
/// バフデバフカードを付けた場合は、最低出品額を増額するか（YES / NO）も選べる。
/// 確定する前なら、選んだカードをすべて手札の元の位置に戻して最初から選び直せる。
/// 横やりがかかっているとバフデバフカードは付けられず、投げ売りがかかっていると最低出品額が値引き額だけ下がる。
/// </summary>
public sealed class ListingSession
{
    readonly GameManager game;
    readonly List<HandCard> buffs = new List<HandCard>();
    readonly List<(HandCard card, int index)> taken = new List<(HandCard card, int index)>();

    public ListingSession(GameManager game, int seller, bool canAttachBuffs = true, int discount = 0)
    {
        this.game = game;
        Seller = seller;
        CanAttachBuffs = canAttachBuffs;
        Discount = discount;
    }

    public int Seller { get; }

    /// <summary>バフデバフカードを付けられるか（横やりがかかっていると false）</summary>
    public bool CanAttachBuffs { get; }

    /// <summary>投げ売りで最低出品額から引く額</summary>
    public int Discount { get; }

    public HandCard Goods { get; private set; }

    public IReadOnlyList<HandCard> Buffs => buffs;

    public bool IsConfirmed { get; private set; }

    /// <summary>増額を選べるか（バフデバフカードを 1 枚以上付けている）</summary>
    public bool CanRaise => buffs.Count > 0;

    /// <summary>増額する（YES）を選んでいるか。付けていなければ常に false</summary>
    public bool IsRaised { get; private set; }

    /// <summary>今の選択で確定したときの最低出品額</summary>
    public int MinPrice => Goods == null ? 0 : Listing.PriceOf(Listing.InitialPriceOf(Goods), IsRaised ? Listing.PriceRaise : 0, Discount);

    public bool CanConfirm => Goods != null && !IsConfirmed;

    /// <summary>やり直せるか（確定する前で、グッズを選んでいる）</summary>
    public bool CanReset => Goods != null && !IsConfirmed;

    public event Action Changed;

    public bool TrySelectGoods(HandCard card)
    {
        if (IsConfirmed || Goods != null || !card.IsGoods || !TryTake(card))
            return false;

        Goods = card;
        Changed?.Invoke();
        return true;
    }

    public bool TryAttachBuff(HandCard card)
    {
        if (IsConfirmed || Goods == null || !CanAttachBuffs || card.IsGoods || !TryTake(card))
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

    /// <summary>選んだグッズと付けたバフデバフカードを手札の元の位置に戻し（並びも選ぶ前に戻る）、増額もしないに戻す</summary>
    public bool TryReset()
    {
        if (!CanReset)
            return false;

        for (int i = taken.Count - 1; i >= 0; i--)
            game.ReturnToHand(Seller, taken[i].card, taken[i].index);
        taken.Clear();
        Goods = null;
        buffs.Clear();
        IsRaised = false;
        Changed?.Invoke();
        return true;
    }

    /// <summary>手札からカードを取り、やり直しで戻せるよう手札の何番目にあったかを覚える</summary>
    bool TryTake(HandCard card)
    {
        int index = game.TryGet(Seller, out PlayerState seller) ? IndexOf(seller.Hand, card) : -1;
        if (index < 0 || !game.RemoveFromHand(Seller, card))
            return false;

        taken.Add((card, index));
        return true;
    }

    static int IndexOf(IReadOnlyList<HandCard> hand, HandCard card)
    {
        for (int i = 0; i < hand.Count; i++)
        {
            if (hand[i] == card)
                return i;
        }
        return -1;
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
        return new Listing(Seller, Goods, buffs.ToArray(), IsRaised, Discount);
    }
}
