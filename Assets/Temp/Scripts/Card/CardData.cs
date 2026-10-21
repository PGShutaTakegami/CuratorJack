using UnityEngine;

/// <summary>グッズカードのレアリティ（同じカテゴリの中での希少さ）</summary>
public enum GoodsRarity
{
    Low,
    Middle,
    Highest
}

/// <summary>
/// カード 1 枚分のデータ（カード名・画像・説明・初期価格）とレアリティ・効果をアセットとして保持する。
/// </summary>
[CreateAssetMenu(fileName = "CardData", menuName = "CJ/CardData")]
public class CardData : ScriptableObject
{
    [SerializeField] private CardManager.cardparameter parameter;

    [Tooltip("グッズカードのレアリティ")]
    [SerializeField] private GoodsRarity rarity;

    [Tooltip("効果カード・スペシャルカードの効果（グッズカードは None）")]
    [SerializeField] private CardEffect effect;

    public CardManager.cardparameter Parameter => parameter;

    public GoodsRarity Rarity => rarity;

    public CardEffect Effect => effect;
}
