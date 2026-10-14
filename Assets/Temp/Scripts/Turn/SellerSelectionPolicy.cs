using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 参加しているプレイヤーの中から、出品者をランダムに 1 人選ぶ。
/// </summary>
public static class SellerSelectionPolicy
{
    public static int Choose(IEnumerable<int> playerNumbers, System.Random random)
    {
        int[] candidates = playerNumbers.ToArray();
        if (candidates.Length == 0)
            return PlayerRegistry.NoPlayer;

        return candidates[random.Next(candidates.Length)];
    }
}
