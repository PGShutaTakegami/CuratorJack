using System.Collections.Generic;

/// <summary>
/// グッズカードを候補から等確率で 1 枚ずつ選ぶ。最高レアリティは決めた枚数までしか選ばない。
/// </summary>
public class GoodsDrawRule
{
    public const int Unlimited = int.MaxValue;

    readonly int maxHighest;
    readonly System.Random random;
    int highestCount;

    public GoodsDrawRule(int maxHighest, System.Random random)
    {
        this.maxHighest = maxHighest;
        this.random = random;
    }

    /// <summary>選べるカードがなければ null を返す。</summary>
    public CardData Pick(IReadOnlyList<CardData> pool)
    {
        var candidates = new List<CardData>();
        foreach (CardData card in pool)
        {
            if (card != null && (card.Rarity != GoodsRarity.Highest || highestCount < maxHighest))
                candidates.Add(card);
        }
        if (candidates.Count == 0)
            return null;

        CardData picked = candidates[random.Next(candidates.Count)];
        if (picked.Rarity == GoodsRarity.Highest)
            highestCount++;
        return picked;
    }
}
