using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 生きている全プレイヤーの手札（GameManager の状態）へカードを配る。
/// 最初のドローは、ゲーム開始時のドローフェーズの告知が出てから一定時間後に firstBatches の通りに配る。
/// 1 周ごとのドローは、ターンの進行から DrawRound で roundBatches の通りに配る。
/// 1 枚ずつ全員に配り、自分の手札のスライドが終わってから次の 1 枚を配る。
/// グッズカードの中身は GoodsCardDeck、バフデバフカードの中身は EffectCardDeck で決める。
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

    [Tooltip("1 回分を引き終えてから次の回を引くまでの秒数")]
    [SerializeField] private float batchInterval = 1f;

    [Tooltip("1 枚のスライドが終わってから次の 1 枚を引くまでの秒数")]
    [SerializeField] private float drawInterval = 0.15f;

    bool firstDrawStarted;

    /// <summary>最初のドローを配り終えた（最後のカードのスライドが終わった時点）</summary>
    public event Action DrawFinished;

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

    /// <summary>1 周ごとのドロー。生きている全員に roundBatches の通りに配る。</summary>
    public IEnumerator DrawRound()
    {
        yield return Deal(roundBatches, _ => goodsDeck.CreateRule());
    }

    IEnumerator Deal(DrawBatch[] dealBatches, Func<int, GoodsDrawRule> ruleOf)
    {
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
                    game.AddToHand(player, CreateCard(dealBatches[i].kind, rules[player]));

                yield return new WaitWhile(() => localHand.IsAnimating);
            }
        }
    }

    HandCard CreateCard(CardManager.CardKinds kind, GoodsDrawRule goodsRule)
    {
        CardData data = kind switch
        {
            CardManager.CardKinds.GoodCard => goodsDeck.Draw(goodsRule),
            CardManager.CardKinds.EffectCard => effectDeck.Draw(),
            _ => null
        };
        return new HandCard(kind, data);
    }
}
