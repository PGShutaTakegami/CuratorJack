using System.Collections.Generic;

/// <summary>
/// プレイヤー 1 人分のゲーム状態（コイン・見送り権・生死・手札・落札した商品・かかっているバフデバフ）。
/// 書き換えは GameManager を通して行い、変更の通知も GameManager が出す。
/// </summary>
public sealed class PlayerState
{
    readonly List<HandCard> hand = new List<HandCard>();
    readonly List<HandCard> collection = new List<HandCard>();
    readonly List<HandCard> activeBuffs = new List<HandCard>();

    public PlayerState(int playerNumber, int coins, int passRights)
    {
        PlayerNumber = playerNumber;
        Coins = coins;
        PassRights = passRights;
    }

    public int PlayerNumber { get; }

    public int Coins { get; private set; }

    public int PassRights { get; private set; }

    public bool IsAlive { get; private set; } = true;

    public IReadOnlyList<HandCard> Hand => hand;

    /// <summary>落札した商品（手札には戻らないため、自分では出品できない）</summary>
    public IReadOnlyList<HandCard> Collection => collection;

    public IReadOnlyList<HandCard> ActiveBuffs => activeBuffs;

    public bool HasGoods => hand.Exists(card => card.IsGoods);

    internal void AddToHand(HandCard card) => hand.Add(card);

    internal bool RemoveFromHand(HandCard card) => hand.Remove(card);

    internal void AddToCollection(HandCard card) => collection.Add(card);

    internal void AddBuffs(IEnumerable<HandCard> buffs) => activeBuffs.AddRange(buffs);

    internal void AddCoins(int amount) => Coins += amount;

    /// <summary>見送り権を 1 枚使う。もう使えない（0 枚）ときは死亡する。死亡したら true を返す。</summary>
    internal bool ConsumePassRight()
    {
        if (PassRights > 0)
        {
            PassRights--;
            return false;
        }

        IsAlive = false;
        return true;
    }
}
