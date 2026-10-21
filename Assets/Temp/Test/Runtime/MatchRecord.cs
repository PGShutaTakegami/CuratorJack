#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;

/// <summary>シミュレーションした 1 試合分の結果</summary>
public sealed class MatchRecord
{
    public MatchRecord(int index, int players)
    {
        Index = index;
        Players = players;
    }

    public int Index { get; }

    public int Players { get; }

    /// <summary>勝者（いなければ PlayerRegistry.NoPlayer）</summary>
    public int Winner { get; set; } = PlayerRegistry.NoPlayer;

    public int Rounds { get; set; }

    /// <summary>出品者のターン数（追加ターンを含む）</summary>
    public int Turns { get; set; }

    public int Auctions { get; set; }

    /// <summary>落札者が決まったオークション（払えずに購入できなかったものも含む）</summary>
    public int SoldAuctions { get; set; }

    public int TotalPrice { get; set; }

    /// <summary>ゲーム内の経過秒数（速度をかける前の、等倍で遊んだときの長さ）</summary>
    public float GameSeconds { get; set; }

    public float RealSeconds { get; set; }

    /// <summary>決着がつく前に打ち切った</summary>
    public bool TimedOut { get; set; }

    public List<int> DeathOrder { get; } = new List<int>();

    public Dictionary<int, int> FinalCoins { get; } = new Dictionary<int, int>();

    public Dictionary<int, int> Collections { get; } = new Dictionary<int, int>();
}
#endif
