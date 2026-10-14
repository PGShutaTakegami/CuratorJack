using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 購入を選んだプレイヤーだけで行うオークション（リアルタイム）。
/// 入札額は 最低出品額以上・自分の前回の額以上・今の最高額より上・所持コイン以下 でなければならない。
/// 降りたプレイヤーは抜け、最後に残った 1 人が落札する（入札していなければ最低出品額で落札）。
/// </summary>
public sealed class AuctionModel
{
    readonly Func<int, int> coinsOf;
    readonly List<int> participants;
    readonly Dictionary<int, int> bids = new Dictionary<int, int>();
    readonly HashSet<int> folded = new HashSet<int>();

    public AuctionModel(Listing listing, IEnumerable<int> participants, Func<int, int> coinsOf)
    {
        Listing = listing;
        this.participants = participants.ToList();
        this.coinsOf = coinsOf;
    }

    public Listing Listing { get; }

    public IReadOnlyList<int> Participants => participants;

    public int HighestBid => bids.Count == 0 ? 0 : bids.Values.Max();

    public IEnumerable<int> ActivePlayers => participants.Where(player => !folded.Contains(player));

    public bool IsFinished => ActivePlayers.Count() <= 1;

    public int Winner => IsFinished ? ActivePlayers.DefaultIfEmpty(PlayerRegistry.NoPlayer).First() : PlayerRegistry.NoPlayer;

    public int Price => Winner == PlayerRegistry.NoPlayer ? 0 : Math.Max(Listing.MinPrice, BidOf(Winner));

    public event Action<int, int> BidPlaced;

    public event Action<int> Folded;

    public bool IsActive(int player) => participants.Contains(player) && !folded.Contains(player) && !IsFinished;

    public int BidOf(int player) => bids.TryGetValue(player, out int bid) ? bid : 0;

    public int CoinsOf(int player) => coinsOf(player);

    /// <summary>入札額として選べる下限（最低出品額と自分の前回の額の大きいほう）</summary>
    public int LowestSelectable(int player) => Math.Max(Listing.MinPrice, BidOf(player));

    public bool CanBid(int player, int amount)
    {
        return IsActive(player)
            && amount >= LowestSelectable(player)
            && amount > HighestBid
            && amount <= coinsOf(player);
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
