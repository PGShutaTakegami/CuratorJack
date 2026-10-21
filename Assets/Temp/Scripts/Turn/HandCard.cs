/// <summary>
/// プレイヤーの手札にあるカード 1 枚。種類と、反映するデータ（まだないカードは null）を持つ。
/// 最高レアリティのグッズカードには、スペシャルカードが 1 枚付いている。
/// </summary>
public sealed class HandCard
{
    public HandCard(CardManager.CardKinds kind, CardData data, CardData special = null)
    {
        Kind = kind;
        Data = data;
        Special = special;
    }

    public CardManager.CardKinds Kind { get; }

    public CardData Data { get; }

    /// <summary>付いているスペシャルカード（付いていなければ null）</summary>
    public CardData Special { get; }

    public bool IsGoods => Kind == CardManager.CardKinds.GoodCard;

    public string DisplayName => Data != null ? Data.Parameter.CardName : Kind.ToString();
}
