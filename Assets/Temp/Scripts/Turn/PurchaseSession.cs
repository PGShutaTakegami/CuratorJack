using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 出品者以外の生きているプレイヤーが、購入するか見送るかを決める手続き。全員が決めたら完了。
/// 最低出品額を払えないプレイヤーは購入を選べない。
/// </summary>
public sealed class PurchaseSession
{
    readonly GameManager game;
    readonly Dictionary<int, bool> decisions = new Dictionary<int, bool>();

    public PurchaseSession(GameManager game, Listing listing, IEnumerable<int> candidates)
    {
        this.game = game;
        Listing = listing;
        Candidates = candidates.ToArray();
    }

    public Listing Listing { get; }

    public IReadOnlyList<int> Candidates { get; }

    public bool IsComplete => Candidates.All(decisions.ContainsKey);

    public IEnumerable<int> Buyers => Candidates.Where(player => decisions.TryGetValue(player, out bool buy) && buy);

    /// <summary>決めた（購入なら true）</summary>
    public event Action<int, bool> Decided;

    public bool HasDecided(int player) => decisions.ContainsKey(player);

    public bool CanBuy(int player)
    {
        return game.TryGet(player, out PlayerState state) && state.Coins >= Listing.MinPrice;
    }

    public bool TryDecide(int player, bool buy)
    {
        if (!Candidates.Contains(player) || decisions.ContainsKey(player) || (buy && !CanBuy(player)))
            return false;

        decisions[player] = buy;
        Decided?.Invoke(player, buy);
        return true;
    }
}
