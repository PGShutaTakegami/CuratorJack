using System;
using System.Collections;
using System.Linq;
using UnityEngine;

/// <summary>
/// チュートリアルの CPU。出品では手札の最初のグッズをそのまま出品し（AttachInNextListing を受けたときだけ決めた効果カードを付ける）、オークションでは降りる。
/// BidInNextAuction を受けたときだけ、次のオークションで落札を狙い、上回られるたびに決めた回数まで raiseStep ずつ上げて入札し返す（今の最高額を上回れないときは降りる）。
/// 出品・入札・降りるのは、案内を読み終えて進めてよくなってから（mayList / mayAct が true になってから）。
/// </summary>
public class TutorialCpuAgent : MonoBehaviour, IPlayerAgent
{
    [SerializeField] private Vector2 thinkSeconds = new Vector2(0.6f, 1.2f);

    [Tooltip("入札し返すとき、今の最高額に足す額")]
    [SerializeField] private int raiseStep = 5;

    GameManager game;
    IPlayerCommands commands;
    Func<bool> mayList;
    Func<bool> mayAct;
    int bidsNextAuction;
    CardData attachNextListing;
    Coroutine running;

    public int PlayerNumber { get; private set; }

    public void Initialize(int playerNumber, GameManager gameManager, IPlayerCommands playerCommands, Func<bool> canList, Func<bool> canAct)
    {
        PlayerNumber = playerNumber;
        game = gameManager;
        commands = playerCommands;
        mayList = canList;
        mayAct = canAct;
    }

    /// <summary>次に参加するオークションでは降りずに入札する。上回られたら times 回まで入札し返す</summary>
    public void BidInNextAuction(int times = 1) => bidsNextAuction = Math.Max(1, times);

    /// <summary>次の出品では、手札に card があればその効果カードを付けて出品する</summary>
    public void AttachInNextListing(CardData card) => attachNextListing = card;

    public void BeginListing(ListingSession session) => Run(Listing(session));

    public void BeginAuction(AuctionModel auction)
    {
        if (!auction.Participants.Contains(PlayerNumber))
            return;

        int times = bidsNextAuction;
        bidsNextAuction = 0;
        Run(times > 0 ? Bid(auction, times) : Fold());
    }

    public void EndPhase()
    {
        if (running != null)
            StopCoroutine(running);
        running = null;
    }

    void Run(IEnumerator routine)
    {
        EndPhase();
        running = StartCoroutine(routine);
    }

    WaitForSeconds Think() => new WaitForSeconds(UnityEngine.Random.Range(thinkSeconds.x, thinkSeconds.y));

    IEnumerator Listing(ListingSession session)
    {
        yield return new WaitUntil(mayList);
        yield return Think();
        if (session.Goods == null && game.TryGet(PlayerNumber, out PlayerState me))
        {
            HandCard goods = me.Hand.FirstOrDefault(card => card.IsGoods);
            if (goods != null)
                commands.SelectGoods(PlayerNumber, goods);
        }

        CardData attach = attachNextListing;
        attachNextListing = null;
        if (attach != null && game.TryGet(PlayerNumber, out PlayerState seller))
        {
            HandCard buff = seller.Hand.FirstOrDefault(card => !card.IsGoods && card.Data == attach);
            if (buff != null)
            {
                yield return Think();
                commands.AttachBuff(PlayerNumber, buff);
            }
        }

        yield return Think();
        commands.Confirm(PlayerNumber);
    }

    IEnumerator Bid(AuctionModel auction, int times)
    {
        for (int i = 0; i < times; i++)
        {
            yield return new WaitUntil(() => !auction.IsActive(PlayerNumber) || (!auction.IsLeading(PlayerNumber) && mayAct()));
            if (!auction.IsActive(PlayerNumber))
                yield break;

            yield return Think();
            int amount = auction.HighestBid > 0
                ? auction.HighestBid + raiseStep
                : auction.LowestSelectable(PlayerNumber);
            amount = Math.Max(auction.LowestSelectable(PlayerNumber), amount);
            if (!auction.CanBid(PlayerNumber, amount))
            {
                commands.Fold(PlayerNumber);
                yield break;
            }
            commands.Bid(PlayerNumber, amount);
            yield return new WaitUntil(() => !auction.IsActive(PlayerNumber) || auction.HighestBid >= amount);
        }
    }

    IEnumerator Fold()
    {
        yield return new WaitUntil(mayAct);
        yield return Think();
        commands.Fold(PlayerNumber);
    }
}
