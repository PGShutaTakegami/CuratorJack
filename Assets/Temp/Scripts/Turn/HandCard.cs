/// <summary>
/// プレイヤーの手札にあるカード 1 枚。種類と、反映するデータ（まだないカードは null）を持つ。
/// </summary>
public sealed class HandCard
{
    public HandCard(CardManager.CardKinds kind, CardData data)
    {
        Kind = kind;
        Data = data;
    }

    public CardManager.CardKinds Kind { get; }

    public CardData Data { get; }

    public bool IsGoods => Kind == CardManager.CardKinds.GoodCard;

    public string DisplayName => Data != null ? Data.Parameter.CardName : Kind.ToString();
}
