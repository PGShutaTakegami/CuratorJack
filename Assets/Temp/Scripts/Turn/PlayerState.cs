using System.Collections.Generic;

/// <summary>
/// プレイヤー 1 人分のゲーム状態（コイン・見送り権・生死・手札・落札した商品・かかっているバフデバフ・持続効果）。
/// 書き換えは GameManager を通して行い、変更の通知も GameManager が出す。
/// </summary>
public sealed class PlayerState
{
    readonly List<HandCard> hand = new List<HandCard>();
    readonly List<HandCard> collection = new List<HandCard>();
    readonly List<HandCard> activeBuffs = new List<HandCard>();
    readonly Dictionary<PlayerStatus, int> statuses = new Dictionary<PlayerStatus, int>();

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

    internal void AddPassRights(int amount) => PassRights += amount;

    /// <summary>持続効果の残り（回数か額）。なければ 0</summary>
    public int StatusOf(PlayerStatus status) => statuses.TryGetValue(status, out int value) ? value : 0;

    internal void AddStatus(PlayerStatus status, int amount) => statuses[status] = StatusOf(status) + amount;

    /// <summary>持続効果を amount だけ使う（残りより多くは使えない）。使えた量を返す</summary>
    internal int UseStatus(PlayerStatus status, int amount)
    {
        int used = System.Math.Min(StatusOf(status), amount);
        statuses[status] = StatusOf(status) - used;
        return used;
    }

    /// <summary>見送り権を count 枚使う。count 枚に足りないときは使い切って死亡する。死亡したら true を返す。</summary>
    internal bool ConsumePassRights(int count)
    {
        if (PassRights >= count)
        {
            PassRights -= count;
            return false;
        }

        PassRights = 0;
        IsAlive = false;
        return true;
    }

    /// <summary>リタイアする（見送り権を手放して死亡する）</summary>
    internal void Retire()
    {
        PassRights = 0;
        IsAlive = false;
    }
}
