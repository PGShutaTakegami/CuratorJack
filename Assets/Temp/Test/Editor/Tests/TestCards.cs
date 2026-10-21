using System.Linq;
using NUnit.Framework;
using UnityEditor;

/// <summary>テストで使う、プロジェクトにある実際のカードデータ</summary>
public static class TestCards
{
    static CardData[] all;

    static CardData[] All => all ??= AssetDatabase.FindAssets("t:CardData")
        .Select(guid => AssetDatabase.LoadAssetAtPath<CardData>(AssetDatabase.GUIDToAssetPath(guid)))
        .Where(card => card != null)
        .ToArray();

    public static CardData WithEffect(CardEffect effect)
    {
        CardData card = All.FirstOrDefault(data => data.Effect == effect);
        Assume.That(card, Is.Not.Null, $"効果 {effect} のカードデータがありません");
        return card;
    }

    public static CardData GoodsData(GoodsRarity rarity)
    {
        CardData card = All.FirstOrDefault(data => data.Effect == CardEffect.None && data.Rarity == rarity && data.Parameter.InitialPrice > 0);
        Assume.That(card, Is.Not.Null, $"レアリティ {rarity} のグッズカードデータがありません");
        return card;
    }

    public static HandCard Goods(GoodsRarity rarity = GoodsRarity.Low) => new HandCard(CardManager.CardKinds.GoodCard, GoodsData(rarity));

    public static HandCard Effect(CardEffect effect) => new HandCard(CardManager.CardKinds.EffectCard, WithEffect(effect));

    /// <summary>データのない（価格 0・刻み 1 の）グッズカード。ルールだけを確かめるときに使う</summary>
    public static HandCard BlankGoods() => new HandCard(CardManager.CardKinds.GoodCard, null);
}
