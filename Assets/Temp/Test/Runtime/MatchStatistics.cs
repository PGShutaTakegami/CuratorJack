#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

/// <summary>シミュレーションした試合の結果を集計し、要約と CSV にする</summary>
public sealed class MatchStatistics
{
    readonly List<MatchRecord> records = new List<MatchRecord>();

    public IReadOnlyList<MatchRecord> Records => records;

    public void Add(MatchRecord record) => records.Add(record);

    public string Summary()
    {
        MatchRecord[] finished = records.Where(record => !record.TimedOut).ToArray();
        var text = new StringBuilder();
        text.AppendLine($"[Test] シミュレーション結果：{records.Count} 試合（打ち切り {records.Count - finished.Length}）");
        if (finished.Length == 0)
            return text.ToString();

        int auctions = finished.Sum(record => record.Auctions);
        int sold = finished.Sum(record => record.SoldAuctions);
        text.AppendLine($"平均ラウンド {finished.Average(record => record.Rounds):0.0}　平均ターン {finished.Average(record => record.Turns):0.0}　平均の長さ（等倍）{finished.Average(record => record.GameSeconds) / 60f:0.0} 分");
        text.AppendLine($"落札率 {Percent(sold, auctions)}（{sold}/{auctions}）　平均落札額 {(sold == 0 ? 0f : (float)finished.Sum(record => record.TotalPrice) / sold):0.0}");

        IEnumerable<int> players = finished.SelectMany(record => record.FinalCoins.Keys).Distinct().OrderBy(player => player);
        foreach (int player in players)
        {
            int wins = finished.Count(record => record.Winner == player);
            double coins = finished.Where(record => record.FinalCoins.ContainsKey(player)).Average(record => record.FinalCoins[player]);
            text.AppendLine($"Player{player}：勝率 {Percent(wins, finished.Length)}（{wins} 勝）　平均最終コイン {coins:0.0}");
        }
        return text.ToString();
    }

    public string ToCsv()
    {
        int maxPlayers = records.Count == 0 ? 0 : records.Max(record => record.Players);
        var csv = new StringBuilder();
        csv.Append("match,players,winner,rounds,turns,auctions,sold,total_price,game_seconds,real_seconds,timed_out,death_order");
        for (int player = 1; player <= maxPlayers; player++)
            csv.Append($",p{player}_coins,p{player}_collection");
        csv.AppendLine();

        foreach (MatchRecord record in records)
        {
            csv.Append(string.Join(",",
                record.Index,
                record.Players,
                record.Winner,
                record.Rounds,
                record.Turns,
                record.Auctions,
                record.SoldAuctions,
                record.TotalPrice,
                record.GameSeconds.ToString("0.0", CultureInfo.InvariantCulture),
                record.RealSeconds.ToString("0.0", CultureInfo.InvariantCulture),
                record.TimedOut ? 1 : 0,
                string.Join(" ", record.DeathOrder)));
            for (int player = 1; player <= maxPlayers; player++)
            {
                string coins = record.FinalCoins.TryGetValue(player, out int c) ? c.ToString() : string.Empty;
                string collection = record.Collections.TryGetValue(player, out int n) ? n.ToString() : string.Empty;
                csv.Append($",{coins},{collection}");
            }
            csv.AppendLine();
        }
        return csv.ToString();
    }

    static string Percent(int count, int total) => total == 0 ? "-" : $"{100f * count / total:0.0}%";
}
#endif
