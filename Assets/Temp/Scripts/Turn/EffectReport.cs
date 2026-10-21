using System.Collections.Generic;

/// <summary>
/// 効果を処理した結果のうち、画面で見せる・進行で行うもの（カードの公開・所持コインの変化・追加のドロー・出品ターンの逆転・メッセージ）。
/// 効果による状態の書き換えは CardEffectResolver が済ませており、ここには演出と進行に必要なことだけを入れる。
/// </summary>
public sealed class EffectReport
{
    /// <summary>1 人分の公開（同時に並べて見せるカード）</summary>
    public sealed class Reveal
    {
        public Reveal(int owner, string title, IReadOnlyList<CardData> cards, IReadOnlyList<CardData> from)
        {
            Owner = owner;
            Title = title;
            Cards = cards;
            From = from;
        }

        public int Owner { get; }

        public string Title { get; }

        public IReadOnlyList<CardData> Cards { get; }

        /// <summary>ディゾルブで Cards に変わる前に見せておくカード（Cards と同じ並び）。null なら下地のグレーのカード</summary>
        public IReadOnlyList<CardData> From { get; }
    }

    /// <summary>効果による追加のドロー 1 枚</summary>
    public readonly struct Draw
    {
        public Draw(int player, CardManager.CardKinds kind)
        {
            Player = player;
            Kind = kind;
        }

        public int Player { get; }

        public CardManager.CardKinds Kind { get; }
    }

    /// <summary>効果で所持コインが変わった 1 人分（リールで前の額から後の額まで巻いて見せる）</summary>
    public readonly struct CoinChange
    {
        public CoinChange(int player, int before, int after)
        {
            Player = player;
            Before = before;
            After = after;
        }

        public int Player { get; }

        public int Before { get; }

        public int After { get; }
    }

    readonly List<Reveal> reveals = new List<Reveal>();
    readonly List<CoinChange> coinChanges = new List<CoinChange>();
    readonly List<Draw> draws = new List<Draw>();
    readonly List<string> messages = new List<string>();

    public IReadOnlyList<Reveal> Reveals => reveals;

    public IReadOnlyList<CoinChange> CoinChanges => coinChanges;

    public IReadOnlyList<Draw> Draws => draws;

    public IReadOnlyList<string> Messages => messages;

    /// <summary>出品ターンの向きを逆にする回数</summary>
    public int Reversals { get; private set; }

    public bool IsEmpty => reveals.Count == 0 && coinChanges.Count == 0 && draws.Count == 0 && messages.Count == 0 && Reversals == 0;

    internal void AddReveal(int owner, string title, IReadOnlyList<CardData> cards) => reveals.Add(new Reveal(owner, title, cards, null));

    /// <summary>from のカードを見せてから、ディゾルブで cards のカードへ変えて公開する</summary>
    internal void AddTransform(int owner, string title, IReadOnlyList<CardData> from, IReadOnlyList<CardData> cards) => reveals.Add(new Reveal(owner, title, cards, from));

    internal void AddCoinChange(int player, int before, int after) => coinChanges.Add(new CoinChange(player, before, after));

    internal void AddDraw(int player, CardManager.CardKinds kind) => draws.Add(new Draw(player, kind));

    internal void AddMessage(string message) => messages.Add(message);

    internal void AddReversal() => Reversals++;
}
