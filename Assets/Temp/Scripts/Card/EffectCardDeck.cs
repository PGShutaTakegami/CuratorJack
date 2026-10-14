using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// バフデバフ（効果）カードの山札。引くたびにランダムなカードデータを返す（効果の中身はまだない）。
/// </summary>
public class EffectCardDeck : MonoBehaviour
{
    [Tooltip("引く候補になる効果カードのデータ")]
    [SerializeField] private List<CardData> effects = new List<CardData>();

    public CardData Draw()
    {
        return effects.Count == 0 ? null : effects[Random.Range(0, effects.Count)];
    }
}
