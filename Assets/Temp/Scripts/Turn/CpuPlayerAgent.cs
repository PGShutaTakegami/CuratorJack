using System.Collections;
using System.Linq;
using UnityEngine;

/// <summary>
/// コンピューターの操作役（仮の思考）。
/// 出品：ランダムなグッズを選び、ときどきバフデバフを付ける（付けたら一定の確率で増額する）。
/// オークション：払えるなら一定の確率で参加し、参加しないなら降りる。参加したら自分なりの上限額までは間をおいて入札し、
/// 上回られて上限を超えるなら降りる（見送り権がなければ必ず参加し、所持コインまで入札する）。
/// 出品者のコインを削れるオークション（最後の 1v1 で 2 人とも見送り権がない）では、自分のほうがコインに余裕があれば、出品者のコインを削り切れる額まで入札する。
/// 入札額は自分と同じく、0 からレアリティの刻みで変えて選べる額（刻みの倍数）だけを使う。
/// 入札は、入札を映す演出が終わってから考え始め、送る直前にも演出中でないことを確かめる（演出は最後の入札者しか映さないため、
/// 演出中に入札が重なると間の人が飛ばされ、同じ人が続けて吊り上げたように見える）。
/// 操作は窓口（IPlayerCommands）へ送る（ネットワークでは切断した人の代わりにホストで動き、操作は全員へ配られる）。
/// </summary>
public class CpuPlayerAgent : MonoBehaviour, IPlayerAgent
{
    [SerializeField] private Vector2 thinkSeconds = new Vector2(0.6f, 1.6f);

    [Tooltip("払えるときにオークションに参加する確率（参加しなければ降りる）")]
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
    IPlayerCommands commands;
    System.Func<bool> isPresentingBid;
    Coroutine running;

    public int PlayerNumber { get; private set; }

    /// <param name="presentingBid">入札を映す演出の最中か（演出中は入札しない）</param>
    public void Initialize(int playerNumber, GameManager gameManager, IPlayerCommands playerCommands, System.Func<bool> presentingBid)
    {
        PlayerNumber = playerNumber;
        game = gameManager;
        commands = playerCommands;
        isPresentingBid = presentingBid ?? (() => false);
    }

    public void BeginListing(ListingSession session) => Run(Listing(session));

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
        if (session.Goods == null)
        {
            HandCard[] goods = me.Hand.Where(card => card.IsGoods).ToArray();
            if (goods.Length == 0)
                yield break;
            commands.SelectGoods(PlayerNumber, goods[Random.Range(0, goods.Length)]);
        }

        HandCard[] buffs = me.Hand.Where(card => !card.IsGoods).ToArray();
        if (session.CanAttachBuffs && buffs.Length > 0 && Random.value < buffChance)
        {
            yield return Think();
            commands.AttachBuff(PlayerNumber, buffs[Random.Range(0, buffs.Length)]);
            commands.SetRaise(PlayerNumber, Random.value < raiseChance);
        }

        yield return Think();
        commands.Confirm(PlayerNumber);
    }

    IEnumerator Auction(AuctionModel auction)
    {
        bool mustStay = game.TryGet(PlayerNumber, out PlayerState me) && me.PassRights == 0;
        int coins = auction.CoinsOf(PlayerNumber);
        bool joins = coins >= auction.Listing.MinPrice && (mustStay || Random.value < buyChance);
        int valuation = mustStay
            ? coins
            : Mathf.Min(coins, Mathf.RoundToInt(auction.Listing.MinPrice * Random.Range(valuationRate.x, valuationRate.y)));

        if (!joins)
        {
            yield return Think();
            commands.Fold(PlayerNumber);
            yield break;
        }

        while (auction.IsActive(PlayerNumber))
        {
            yield return new WaitWhile(isPresentingBid);
            yield return Think();
            if (!auction.IsActive(PlayerNumber))
                yield break;
            if (isPresentingBid())
                continue;

            bool isLeading = auction.HighestBid > 0 && auction.BidOf(PlayerNumber) == auction.HighestBid;
            if (isLeading)
                continue;

            int next = DrainBid(auction, auction.Listing.RoundUpToStep(Mathf.Max(auction.LowestSelectable(PlayerNumber), auction.HighestBid + 1)), coins);
            if (next <= valuation && auction.CanBid(PlayerNumber, next))
                commands.Bid(PlayerNumber, next);
            else
                commands.Fold(PlayerNumber);
        }
    }

    /// <summary>
    /// 出品者のコインを削れるオークションでは、払った後も出品者より多くコインが残るとき、出品者のコインを削り切れる額まで（払える範囲で）上げる。
    /// 削った分と多く払った分は同じ額なので、残りのコインの差は変わらず、先に尽きるのは出品者になる。
    /// </summary>
    int DrainBid(AuctionModel auction, int next, int coins)
    {
        int sellerCoins = auction.CoinsOf(auction.Listing.Seller);
        if (!auction.DrainsSeller || coins - auction.Listing.MinPrice <= sellerCoins)
            return next;

        int drainAll = auction.Listing.RoundUpToStep(auction.Listing.MinPrice + sellerCoins);
        int affordable = coins / auction.Listing.BidStep * auction.Listing.BidStep;
        return Mathf.Max(next, Mathf.Min(drainAll, affordable));
    }
}
