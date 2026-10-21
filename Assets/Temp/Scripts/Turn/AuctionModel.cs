using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 出品者以外の生きているプレイヤー全員で行うオークション（リアルタイム）。各自が入札するか降りるかを選ぶ。
/// 入札額は 最低出品額以上・自分の前回の額以上・今の最高額より上・所持コイン + 前借りできる額以下 でなければならない。
/// 最高額を付けているプレイヤーも、さらに上の額で入札し直せる。
/// 降りたプレイヤーは抜け、残りが 1 人でその人が最高額を入札していれば、その額で落札する。全員が降りたら落札者なし。
/// 落札額が所持コインより多いと購入できず、見送り権を ShortfallPenalty 枚失う（扱いは TurnFlowController が行う）。
/// </summary>
public sealed class AuctionModel
{
    readonly Func<int, int> coinsOf;
    readonly List<int> participants;
    readonly Dictionary<int, int> bids = new Dictionary<int, int>();
    readonly HashSet<int> folded = new HashSet<int>();

    /// <param name="overdraft">所持コインより多く入札できる額</param>
    /// <param name="shortfallPenalty">落札額を払えなかったときに失う見送り権の枚数</param>
    public AuctionModel(Listing listing, IEnumerable<int> participants, Func<int, int> coinsOf, int overdraft, int shortfallPenalty)
    {
        Listing = listing;
        this.participants = participants.ToList();
        this.coinsOf = coinsOf;
        Overdraft = overdraft;
        ShortfallPenalty = shortfallPenalty;
    }

    public Listing Listing { get; }

    public int Overdraft { get; }

    public int ShortfallPenalty { get; }

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

    /// <summary>入札額として選べる上限（所持コイン + 前借りできる額）</summary>
    public int HighestSelectable(int player) => Math.Max(0, coinsOf(player)) + Overdraft;

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

    public bool TryFold(int player)
    {
        if (!IsActive(player))
            return false;

        folded.Add(player);
        Folded?.Invoke(player);
        return true;
    }
}
