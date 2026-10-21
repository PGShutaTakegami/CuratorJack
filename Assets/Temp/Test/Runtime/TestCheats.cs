#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Linq;
using UnityEngine;

/// <summary>
/// 起きたことにして状態を書き換える（コイン・見送り権・持続効果・手札・リタイア・待ち時間切れ）。
/// 書き換えは GameManager と操作の窓口を通すので、画面の表示や進行も実際と同じように反応する。
/// ネットワークでは全員の画面の状態がずれるため、SinglePlay のときだけ使える。
/// </summary>
public static class TestCheats
{
    const int FireSaleDiscount = 5;

    public static bool CanUse(TestSceneRefs refs) => refs.IsSinglePlay && refs.HasStarted;

    public static void AddCoins(TestSceneRefs refs, int player, int amount)
    {
        if (CanUse(refs))
            refs.Game.AddCoins(player, amount);
    }

    /// <summary>増やすか、使ったことにする（足りなければ脱落する）</summary>
    public static void AddPassRights(TestSceneRefs refs, int player, int amount)
    {
        if (!CanUse(refs))
            return;

        if (amount >= 0)
            refs.Game.AddPassRights(player, amount);
        else
            refs.Game.ConsumePassRight(player, -amount);
    }

    public static void AddStatus(TestSceneRefs refs, int player, PlayerStatus status)
    {
        if (CanUse(refs))
            refs.Game.AddStatus(player, status, status == PlayerStatus.FireSaleDiscount ? FireSaleDiscount : 1);
    }

    /// <summary>カードを手札に加える。最高レアリティのグッズにはスペシャルカードを付ける</summary>
    public static void GiveCard(TestSceneRefs refs, int player, CardData card)
    {
        if (!CanUse(refs) || card == null)
            return;

        bool isGoods = refs.GoodsDeck != null && refs.GoodsDeck.Goods.Contains(card);
        CardData special = isGoods && card.Rarity == GoodsRarity.Highest && refs.SpecialDeck != null ? refs.SpecialDeck.Draw() : null;
        var kind = isGoods ? CardManager.CardKinds.GoodCard : CardManager.CardKinds.EffectCard;
        refs.Game.AddToHand(player, new HandCard(kind, card, special));
    }

    public static void Retire(TestSceneRefs refs, int player)
    {
        if (CanUse(refs))
            refs.Flow.Commands.Retire(player);
    }

    /// <summary>今の出品・オークションを待ち時間切れにする（出品はランダムなグッズで確定、オークションは最高額の人以外が降りる）</summary>
    public static void TimeOutNow(TestSceneRefs refs)
    {
        if (!CanUse(refs))
            return;

        refs.Flow.Timeouts.TimeOut(refs.LocalPlayer);
        Debug.Log("[Test] 待ち時間切れにしました");
    }
}
#endif
