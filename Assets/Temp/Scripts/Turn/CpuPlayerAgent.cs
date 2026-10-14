using System.Collections;
using System.Linq;
using UnityEngine;

/// <summary>
/// コンピューターの操作役（仮の思考）。
/// 出品：ランダムなグッズを選び、ときどきバフデバフを付ける（付けたら一定の確率で増額する）。
/// 購入：払えるなら一定の確率で購入する（見送り権がなければ払える限り購入する）。
/// オークション：自分なりの上限額までは間をおいて入札し、上回られて上限を超えるなら降りる。
/// </summary>
public class CpuPlayerAgent : MonoBehaviour, IPlayerAgent
{
    [SerializeField] private Vector2 thinkSeconds = new Vector2(0.6f, 1.6f);

    [Range(0f, 1f)]
    [SerializeField] private float buyChance = 0.6f;

    [Range(0f, 1f)]
    [SerializeField] private float buffChance = 0.4f;

    [Tooltip("バフデバフカードを付けたときに、最低出品額を増額する確率")]
    [Range(0f, 1f)]
    [SerializeField] private float raiseChance = 0.5f;

    [Tooltip("オークションの上限額を、最低出品額の何倍にするか（最小・最大）")]
    [SerializeField] private Vector2 valuationRate = new Vector2(1.2f, 2f);

    GameManager game;
    Coroutine running;

    public int PlayerNumber { get; private set; }

    public void Initialize(int playerNumber, GameManager gameManager)
    {
        PlayerNumber = playerNumber;
        game = gameManager;
    }

    public void BeginListing(ListingSession session) => Run(Listing(session));

    public void BeginPurchase(PurchaseSession session) => Run(Purchase(session));

    public void BeginAuction(AuctionModel auction)
    {
        if (auction.Participants.Contains(PlayerNumber))
            Run(Auction(auction));
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

    WaitForSeconds Think() => new WaitForSeconds(Random.Range(thinkSeconds.x, thinkSeconds.y));

    IEnumerator Listing(ListingSession session)
    {
        if (!game.TryGet(PlayerNumber, out PlayerState me))
            yield break;

        yield return Think();
        HandCard[] goods = me.Hand.Where(card => card.IsGoods).ToArray();
        if (goods.Length == 0)
            yield break;
        session.TrySelectGoods(goods[Random.Range(0, goods.Length)]);

        HandCard[] buffs = me.Hand.Where(card => !card.IsGoods).ToArray();
        if (buffs.Length > 0 && Random.value < buffChance)
        {
            yield return Think();
            session.TryAttachBuff(buffs[Random.Range(0, buffs.Length)]);
            session.TrySetRaise(Random.value < raiseChance);
        }

        yield return Think();
        session.TryConfirm();
    }

    IEnumerator Purchase(PurchaseSession session)
    {
        yield return Think();
        if (!game.TryGet(PlayerNumber, out PlayerState me))
            yield break;

        bool wantsToBuy = session.CanBuy(PlayerNumber) && (me.PassRights == 0 || Random.value < buyChance);
        session.TryDecide(PlayerNumber, wantsToBuy);
    }

    IEnumerator Auction(AuctionModel auction)
    {
        int valuation = Mathf.Min(
            auction.CoinsOf(PlayerNumber),
            Mathf.RoundToInt(auction.Listing.MinPrice * Random.Range(valuationRate.x, valuationRate.y)));

        while (auction.IsActive(PlayerNumber))
        {
            yield return Think();
            if (!auction.IsActive(PlayerNumber))
                yield break;

            bool isLeading = auction.HighestBid > 0 && auction.BidOf(PlayerNumber) == auction.HighestBid;
            if (isLeading)
                continue;

            int next = Mathf.Max(auction.LowestSelectable(PlayerNumber), auction.HighestBid + auction.Listing.BidStep);
            if (next <= valuation && auction.CanBid(PlayerNumber, next))
                auction.TryBid(PlayerNumber, next);
            else
                auction.TryFold(PlayerNumber);
        }
    }
}
