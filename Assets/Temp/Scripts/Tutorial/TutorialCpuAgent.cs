using System;
using System.Collections;
using System.Linq;
using UnityEngine;

/// <summary>
/// チュートリアルの CPU。出品では手札の最初のグッズをそのまま出品し、オークションでは降りる。
/// BidInNextAuction を受けたときだけ、次のオークションで 1 回入札して落札を狙う。
/// 出品・入札・降りるのは、案内を読み終えて進めてよくなってから（mayList / mayAct が true になってから）。
/// </summary>
public class TutorialCpuAgent : MonoBehaviour, IPlayerAgent
{
    [SerializeField] private Vector2 thinkSeconds = new Vector2(0.6f, 1.2f);

    GameManager game;
    IPlayerCommands commands;
    Func<bool> mayList;
    Func<bool> mayAct;
    bool bidsNextAuction;
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

    /// <summary>次に参加するオークションでは降りずに入札する</summary>
    public void BidInNextAuction() => bidsNextAuction = true;

    public void BeginListing(ListingSession session) => Run(Listing(session));

    public void BeginAuction(AuctionModel auction)
    {
        if (!auction.Participants.Contains(PlayerNumber))
            return;

        bool bids = bidsNextAuction;
        bidsNextAuction = false;
        Run(bids ? Bid(auction) : Fold());
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

        yield return Think();
        commands.Confirm(PlayerNumber);
    }

    IEnumerator Bid(AuctionModel auction)
    {
        yield return new WaitUntil(mayAct);
        yield return Think();
        commands.Bid(PlayerNumber, Math.Max(auction.LowestSelectable(PlayerNumber), auction.HighestBid + 1));
    }

    IEnumerator Fold()
    {
        yield return new WaitUntil(mayAct);
        yield return Think();
        commands.Fold(PlayerNumber);
    }
}
