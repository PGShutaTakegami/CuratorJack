using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 出品者以外の生きているプレイヤー全員で行うオークション（リアルタイム）。各自が入札するか降りるかを選ぶ。
/// 入札額は 最低出品額以上・自分の前回の額以上・今の最高額より上・所持コイン以下 でなければならない。
/// 最高額を付けているプレイヤーも、さらに上の額で入札し直せる。ただし最高額を付けている間は降りられない（吊り上げたまま降りられないように）。
/// 降りたプレイヤーは抜け、残りが 1 人でその人が最高額を入札していれば、その額で落札する。全員が降りたら落札者なし。
/// 脱落したプレイヤーは、最高額を付けていても Withdraw で抜けさせる。
/// DrainsSeller のオークションでは、落札額のうち最低出品額を超えた分だけ出品者のコインを削る（扱いは TurnFlowController が行う）。
/// 落札額が所持コインより多いと購入できず、見送り権を ShortfallPenalty 枚失う（扱いは TurnFlowController が行う。所持コインを超えては入札できないため、通常は起きない）。
/// </summary>
public sealed class AuctionModel
{
    readonly Func<int, int> coinsOf;
    readonly List<int> participants;
    readonly Dictionary<int, int> bids = new Dictionary<int, int>();
    readonly HashSet<int> folded = new HashSet<int>();

    /// <param name="shortfallPenalty">落札額を払えなかったときに失う見送り権の枚数</param>
    /// <param name="drainsSeller">最低出品額より多く入札して落札した分だけ、出品者のコインを削れるか（最後の 1v1 で 2 人とも見送り権がないとき）</param>
    public AuctionModel(Listing listing, IEnumerable<int> participants, Func<int, int> coinsOf, int shortfallPenalty, bool drainsSeller = false)
    {
        Listing = listing;
        this.participants = participants.ToList();
        this.coinsOf = coinsOf;
        ShortfallPenalty = shortfallPenalty;
        DrainsSeller = drainsSeller;
    }

    public Listing Listing { get; }

    public int ShortfallPenalty { get; }

    public bool DrainsSeller { get; }

    /// <summary>落札で出品者のコインから削る額（落札額 − 最低出品額。削れないオークションや落札者がいなければ 0）</summary>
    public int DrainAmount => Winner == PlayerRegistry.NoPlayer ? 0 : DrainOf(Price);

    /// <summary>この額で落札したときに出品者のコインから削る額（削れないオークションなら 0）</summary>
    public int DrainOf(int price) => DrainsSeller ? Math.Max(0, price - Listing.MinPrice) : 0;

    public IReadOnlyList<int> Participants => participants;

    public int HighestBid => bids.Count == 0 ? 0 : bids.Values.Max();

    public IEnumerable<int> ActivePlayers => participants.Where(player => !folded.Contains(player));

    /// <summary>全員が降りた（落札者なし）か、残りが 1 人でその人が最高額を入札している</summary>
    public bool IsFinished
    {
        get
        {
            int[] active = ActivePlayers.ToArray();
            return active.Length == 0 || (active.Length == 1 && IsLeading(active[0]));
        }
    }

    public int Winner => IsFinished ? ActivePlayers.DefaultIfEmpty(PlayerRegistry.NoPlayer).First() : PlayerRegistry.NoPlayer;

    public int Price => Winner == PlayerRegistry.NoPlayer ? 0 : Math.Max(Listing.MinPrice, BidOf(Winner));

    public event Action<int, int> BidPlaced;

    public event Action<int> Folded;

    public bool IsActive(int player) => participants.Contains(player) && !folded.Contains(player) && !IsFinished;

    public int BidOf(int player) => bids.TryGetValue(player, out int bid) ? bid : 0;

    public int CoinsOf(int player) => coinsOf(player);

    /// <summary>入札額として選べる下限（最低出品額と自分の前回の額の大きいほう）</summary>
    public int LowestSelectable(int player) => Math.Max(Listing.MinPrice, BidOf(player));

    /// <summary>入札額として選べる上限（所持コイン）</summary>
    public int HighestSelectable(int player) => Math.Max(0, coinsOf(player));

    /// <summary>今の最高額を付けているのがこのプレイヤーか</summary>
    public bool IsLeading(int player) => HighestBid > 0 && BidOf(player) == HighestBid;

    /// <summary>落札額を所持コインで払えるか</summary>
    public bool CanAfford(int player, int price) => price <= coinsOf(player);

    public bool CanBid(int player, int amount)
    {
        return IsActive(player)
            && amount >= LowestSelectable(player)
            && amount > HighestBid
            && amount <= HighestSelectable(player);
    }

    public bool TryBid(int player, int amount)
    {
        if (!CanBid(player, amount))
            return false;

        bids[player] = amount;
        BidPlaced?.Invoke(player, amount);
        return true;
    }

    /// <summary>降りられるか（最高額を付けている間は降りられない）</summary>
    public bool CanFold(int player) => IsActive(player) && !IsLeading(player);

    public bool TryFold(int player)
    {
        if (!CanFold(player))
            return false;

        Leave(player);
        return true;
    }

    /// <summary>脱落したプレイヤーを抜けさせる（最高額を付けていても抜ける）</summary>
    public bool Withdraw(int player)
    {
        if (!IsActive(player))
            return false;

        Leave(player);
        return true;
    }

    void Leave(int player)
    {
        folded.Add(player);
        Folded?.Invoke(player);
    }
}
