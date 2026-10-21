using System.Collections;
using System.Linq;
using UnityEngine;

/// <summary>
/// コンピューターの操作役（仮の思考）。
/// 出品：ランダムなグッズを選び、ときどきバフデバフを付ける（付けたら一定の確率で増額する）。
/// オークション：払えるなら一定の確率で参加し、参加しないなら降りる。参加したら自分なりの上限額までは間をおいて入札し、
/// 上回られて上限を超えるなら降りる（見送り権がなければ必ず参加し、所持コインまで入札する）。
/// 入札額は自分と同じく、0 からレアリティの刻みで変えて選べる額（刻みの倍数）だけを使う。
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
    Coroutine running;

    public int PlayerNumber { get; private set; }

    public void Initialize(int playerNumber, GameManager gameManager, IPlayerCommands playerCommands)
    {
        PlayerNumber = playerNumber;
        game = gameManager;
        commands = playerCommands;
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
            yield return Think();
            if (!auction.IsActive(PlayerNumber))
                yield break;

            bool isLeading = auction.HighestBid > 0 && auction.BidOf(PlayerNumber) == auction.HighestBid;
            if (isLeading)
                continue;

            int next = auction.Listing.RoundUpToStep(Mathf.Max(auction.LowestSelectable(PlayerNumber), auction.HighestBid + 1));
            if (next <= valuation && auction.CanBid(PlayerNumber, next))
                commands.Bid(PlayerNumber, next);
            else
                commands.Fold(PlayerNumber);
        }
    }
}
