using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 最高レアリティのグッズカードに付けるスペシャルカードの山札。引くたびにランダムなカードデータを返す。Active でないカードは引かず、一覧にも出さない。
/// </summary>
public class SpecialCardDeck : MonoBehaviour
{
    [Tooltip("付ける候補になるスペシャルカードのデータ")]
    [SerializeField] private List<CardData> specials = new List<CardData>();

    System.Random random = new System.Random();

    /// <summary>付く可能性のあるスペシャルカード（一覧表示用）</summary>
    public IReadOnlyList<CardData> Specials => Activatable.ActiveOnly(specials);

    /// <summary>引くカードの乱数をシードからやり直す（全員の画面で同じカードにするため）</summary>
    public void Reseed(int seed)
    {
        random = new System.Random(seed);
    }

    public CardData Draw()
    {
        IReadOnlyList<CardData> pool = Specials;
        return pool.Count == 0 ? null : pool[random.Next(pool.Count)];
    }
}
