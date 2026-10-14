using System;
using System.Collections.Generic;

/// <summary>
/// 出品者の順番（時計回り）と、1 周したかどうかを管理する。死んでいるプレイヤーは飛ばす。
/// </summary>
public sealed class TurnOrder
{
    readonly List<int> clockwise;
    readonly HashSet<int> soldThisRound = new HashSet<int>();

    /// <param name="clockwisePlayers">時計回りに並べたプレイヤー番号</param>
    public TurnOrder(IEnumerable<int> clockwisePlayers)
    {
        clockwise = new List<int>(clockwisePlayers);
    }

    public IReadOnlyList<int> Clockwise => clockwise;

    /// <summary>current の次（時計回り）の生きているプレイヤー。いなければ PlayerRegistry.NoPlayer。</summary>
    public int NextAlive(int current, Func<int, bool> isAlive)
    {
        int start = clockwise.IndexOf(current);
        for (int step = 1; step <= clockwise.Count; step++)
        {
            int candidate = clockwise[(start + step + clockwise.Count) % clockwise.Count];
            if (isAlive(candidate))
                return candidate;
        }
        return PlayerRegistry.NoPlayer;
    }

    public void MarkTurnDone(int seller)
    {
        soldThisRound.Add(seller);
    }

    /// <summary>生きている全員が出品者を 1 回ずつ終えたら true。</summary>
    public bool IsRoundComplete(Func<int, bool> isAlive)
    {
        foreach (int player in clockwise)
        {
            if (isAlive(player) && !soldThisRound.Contains(player))
                return false;
        }
        return true;
    }

    public void StartNewRound()
    {
        soldThisRound.Clear();
    }
}
