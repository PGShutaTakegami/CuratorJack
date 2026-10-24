using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 生きている全プレイヤーの手札（GameManager の状態）へカードを配る。
/// 最初のドローは、ゲーム開始時のドローフェーズの告知が出てから一定時間後に firstBatches の通りに配る。
/// 1 周ごとのドローは、ターンの進行から DrawRound で roundBatches の通りに配る（最後の 1v1 ではバフデバフカードを倍にし、デバフカードだけにする）。
/// 1 枚ずつ全員に配り、自分の手札のスライドが終わってから次の 1 枚を配る。
/// グッズカードの中身は GoodsCardDeck、バフデバフカードの中身は EffectCardDeck で決める。
/// 最高レアリティのグッズカードには、SpecialCardDeck から必ず 1 枚スペシャルカードを付ける。
/// 圧力が残っているプレイヤーは、出品カード（グッズ）を 1 回引けない。効果による追加のドローは DrawExtra で配る。
/// </summary>
public class DrawPhaseController : MonoBehaviour
{
    [Serializable]
    struct DrawBatch
    {
        [Tooltip("引く枚数")]
        public int count;

        public CardManager.CardKinds kind;
    }

    [SerializeField] private GameStartAnnouncer announcer;

    [Tooltip("ドローフェーズの告知の文字")]
    [SerializeField] private GameObject drawPhaseMessage;

    [SerializeField] private GameManager game;

    [Tooltip("自分の手札の表示。スライドが終わるまで次を配らない")]
    [SerializeField] private LocalHandPresenter localHand;

    [Tooltip("引いたグッズカードの中身を決める山札")]
    [SerializeField] private GoodsCardDeck goodsDeck;

    [Tooltip("引いたバフデバフカードの中身を決める山札")]
    [SerializeField] private EffectCardDeck effectDeck;

    [Tooltip("最高レアリティのグッズカードに付けるスペシャルカードの山札")]
    [SerializeField] private SpecialCardDeck specialDeck;

    [Tooltip("告知が出始めてから最初のドローを始めるまでの秒数")]
    [SerializeField] private float delaySeconds = 1f;

    [Tooltip("最初のドローで 1 回ごとに引くカード（順番に引く）")]
    [SerializeField] private DrawBatch[] batches =
    {
        new DrawBatch { count = 3, kind = CardManager.CardKinds.GoodCard },
        new DrawBatch { count = 2, kind = CardManager.CardKinds.EffectCard }
    };

    [Tooltip("1 周ごとのドローで引くカード（順番に引く）")]
    [SerializeField] private DrawBatch[] roundBatches =
    {
        new DrawBatch { count = 1, kind = CardManager.CardKinds.GoodCard },
        new DrawBatch { count = 1, kind = CardManager.CardKinds.EffectCard }
    };

    [Tooltip("最後の 1v1 の 1 周ごとのドローで、バフデバフカードを引く枚数の倍率（引くのはデバフカードだけ）")]
    [SerializeField] private int finalDuelEffectMultiplier = 2;

    [Tooltip("1 回分を引き終えてから次の回を引くまでの秒数")]
    [SerializeField] private float batchInterval = 1f;

    [Tooltip("1 枚のスライドが終わってから次の 1 枚を引くまでの秒数")]
    [SerializeField] private float drawInterval = 0.15f;

    bool firstDrawStarted;
    IScriptedDraw scriptedDraw;

    /// <summary>最初のドローを配り終えた（最後のカードのスライドが終わった時点）</summary>
    public event Action DrawFinished;

    /// <summary>圧力で出品カード（グッズ）を引けなかった（引けなかったプレイヤー番号）</summary>
    public event Action<int> GoodsDrawBlocked;

    void Awake()
    {
        announcer.MessageShown += HandleMessageShown;
    }

    void OnDestroy()
    {
        announcer.MessageShown -= HandleMessageShown;
    }

    void HandleMessageShown(GameObject message)
    {
        if (message != drawPhaseMessage || firstDrawStarted)
            return;

        firstDrawStarted = true;
        StartCoroutine(DrawFirst());
    }

    IEnumerator DrawFirst()
    {
        yield return new WaitForSeconds(delaySeconds);
        yield return new WaitUntil(() => goodsDeck.CanBeginFirstDraw);

        yield return Deal(batches, goodsDeck.CreateFirstDrawRule);
        DrawFinished?.Invoke();
    }

    /// <summary>配るカードを決めておく仕組みを使う。決めていないカードは山札から引く（null で山札だけに戻す）。</summary>
    public void UseScriptedDraw(IScriptedDraw script)
    {
        scriptedDraw = script;
    }

    /// <summary>山札の乱数をシードからやり直す（全員の画面で同じカードを配るため）。山札ごとに別の値を使う。</summary>
    public void Reseed(int seed)
    {
        goodsDeck.Reseed(unchecked(seed + 1));
        effectDeck.Reseed(unchecked(seed + 2));
        specialDeck.Reseed(unchecked(seed + 3));
    }

    /// <summary>
    /// 1 周ごとのドロー。生きている全員に roundBatches の通りに配る。
    /// 最後の 1v1 では、バフデバフカードを finalDuelEffectMultiplier 倍の枚数にし、デバフカードだけから引く。
    /// </summary>
    public IEnumerator DrawRound()
    {
        if (!game.IsFinalDuel)
        {
            yield return Deal(roundBatches, _ => goodsDeck.CreateRule());
            yield break;
        }

        DrawBatch[] duelBatches = roundBatches
            .Select(batch => batch.kind == CardManager.CardKinds.EffectCard
                ? new DrawBatch { count = batch.count * finalDuelEffectMultiplier, kind = batch.kind }
                : batch)
            .ToArray();
        yield return Deal(duelBatches, _ => goodsDeck.CreateRule(), effectDeck.DrawDebuff);
    }

    IEnumerator Deal(DrawBatch[] dealBatches, Func<int, GoodsDrawRule> ruleOf, Func<CardData> drawEffect = null)
    {
        drawEffect ??= effectDeck.Draw;
        int[] receivers = game.AlivePlayers.Select(player => player.PlayerNumber).ToArray();
        Dictionary<int, GoodsDrawRule> rules = receivers.ToDictionary(player => player, ruleOf);

        for (int i = 0; i < dealBatches.Length; i++)
        {
            if (i > 0)
                yield return new WaitForSeconds(batchInterval);

            for (int n = 0; n < dealBatches[i].count; n++)
            {
                if (n > 0)
                    yield return new WaitForSeconds(drawInterval);

                foreach (int player in receivers)
                    Give(player, dealBatches[i].kind, rules[player], drawEffect);

                yield return new WaitWhile(() => localHand.IsAnimating);
            }
        }
    }

    /// <summary>効果による追加のドロー。1 枚引いて、自分の手札のスライドが終わるまで待つ。</summary>
    public IEnumerator DrawExtra(int player, CardManager.CardKinds kind)
    {
        Give(player, kind, goodsDeck.CreateRule(), effectDeck.Draw);
        yield return new WaitWhile(() => localHand.IsAnimating);
    }

    /// <summary>1 枚配る。圧力が残っている人には出品カード（グッズ）を配らず、圧力を 1 回使う。バフデバフカードの中身は drawEffect で決める。</summary>
    void Give(int player, CardManager.CardKinds kind, GoodsDrawRule goodsRule, Func<CardData> drawEffect)
    {
        if (kind == CardManager.CardKinds.GoodCard && game.TryUseStatus(player, PlayerStatus.Pressure))
        {
            Debug.Log($"[Draw] Player{player} は圧力で出品カードを引けませんでした");
            GoodsDrawBlocked?.Invoke(player);
            return;
        }

        game.AddToHand(player, CreateCard(player, kind, goodsRule, drawEffect));
    }

    HandCard CreateCard(int player, CardManager.CardKinds kind, GoodsDrawRule goodsRule, Func<CardData> drawEffect)
    {
        if (scriptedDraw != null && scriptedDraw.TryCreate(player, kind, out HandCard scripted))
            return scripted;

        CardData data = kind switch
        {
            CardManager.CardKinds.GoodCard => goodsDeck.Draw(goodsRule),
            CardManager.CardKinds.EffectCard => drawEffect(),
            _ => null
        };
        bool isHighest = kind == CardManager.CardKinds.GoodCard && data != null && data.Rarity == GoodsRarity.Highest;
        return new HandCard(kind, data, isHighest ? specialDeck.Draw() : null);
    }
}
