using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// バフデバフ（効果）カードの山札。引くたびにランダムなカードデータを返す。Active でないカードは引かず、一覧にも出さない。
/// </summary>
public class EffectCardDeck : MonoBehaviour
{
    [Tooltip("引く候補になる効果カードのデータ")]
    [SerializeField] private List<CardData> effects = new List<CardData>();

    [Tooltip("デバフカード（最後の 1v1 のドローではここからだけ引く）")]
    [SerializeField] private List<CardData> debuffs = new List<CardData>();

    System.Random random = new System.Random();

    /// <summary>引く候補になる効果カード（一覧表示用）</summary>
    public IReadOnlyList<CardData> Effects => Activatable.ActiveOnly(effects);

    /// <summary>引くカードの乱数をシードからやり直す（全員の画面で同じカードにするため）</summary>
    public void Reseed(int seed)
    {
        random = new System.Random(seed);
    }

    public CardData Draw()
    {
        return Pick(Effects);
    }

    /// <summary>デバフカードだけから引く（引けるデバフがなければ通常の候補から引く）</summary>
    public CardData DrawDebuff()
    {
        List<CardData> pool = Activatable.ActiveOnly(debuffs);
        return pool.Count == 0 ? Draw() : Pick(pool);
    }

    CardData Pick(IReadOnlyList<CardData> pool)
    {
        return pool.Count == 0 ? null : pool[random.Next(pool.Count)];
    }
}
