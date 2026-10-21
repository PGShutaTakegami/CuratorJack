using UnityEngine;

/// <summary>
/// ドローで手札に来るグッズカードを決める。
/// 最初のドローでは、最初の出品者には最高レアリティを渡さず、他の人にも最大 firstDrawMaxHighest 枚までにする。
/// それ以外のドローは制限なしのランダム。ルールはプレイヤーごとに作り、そのプレイヤーの 1 回のドロー中だけ使う。
/// </summary>
public class GoodsCardDeck : MonoBehaviour
{
    [SerializeField] private SellerSelector sellerSelector;

    [Tooltip("ドローで来る可能性のあるグッズカード")]
    [SerializeField] private CardData[] goods;

    [Tooltip("最初のドローで、最初の出品者以外に来る最高レアリティの上限枚数")]
    [SerializeField] private int firstDrawMaxHighest = 1;

    System.Random random = new System.Random();

    /// <summary>ドローで来る可能性のあるグッズカード（一覧表示用）</summary>
    public System.Collections.Generic.IReadOnlyList<CardData> Goods => goods;

    /// <summary>引くカードの乱数をシードからやり直す（全員の画面で同じカードにするため）</summary>
    public void Reseed(int seed)
    {
        random = new System.Random(seed);
    }

    /// <summary>最初のドローの条件（最初の出品者か）が決まるには、出品者が決まっている必要がある</summary>
    public bool CanBeginFirstDraw => sellerSelector.CurrentSeller != PlayerRegistry.NoPlayer;

    public GoodsDrawRule CreateFirstDrawRule(int playerNumber)
    {
        bool isFirstSeller = sellerSelector.CurrentSeller == playerNumber;
        return new GoodsDrawRule(isFirstSeller ? 0 : firstDrawMaxHighest, random);
    }

    public GoodsDrawRule CreateRule()
    {
        return new GoodsDrawRule(GoodsDrawRule.Unlimited, random);
    }

    public CardData Draw(GoodsDrawRule rule)
    {
        return rule.Pick(goods);
    }
}
