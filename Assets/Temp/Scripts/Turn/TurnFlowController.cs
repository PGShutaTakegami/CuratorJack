using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 出品者のターンを順に進める。
/// 1 ターン：出品者の演出 → 出品（グッズがなければ見送り権を消費）→ 他の人が購入か見送りを決める
/// → 購入者 0 人ならバフデバフは出品者に、1 人なら最低出品額で即購入、2 人以上ならオークション。
/// 生きている全員が 1 回ずつ出品者を終えたらドローし、時計回りで次の生きているプレイヤーへ。最後の 1 人が勝者。
/// 操作役（IPlayerAgent）を Activate で渡されるまでは、最初の出品者の演出だけを行う。
/// </summary>
public class TurnFlowController : MonoBehaviour
{
    [SerializeField] private SellerSelector selector;

    [SerializeField] private GameManager game;

    [SerializeField] private PlayerRegistry registry;

    [SerializeField] private SellerTurnPresenter sellerPresenter;

    [SerializeField] private DrawPhaseController drawPhase;

    [SerializeField] private GameStartAnnouncer announcer;

    [Tooltip("1 周ごとのドローの前に出す告知の文字")]
    [SerializeField] private GameObject drawPhaseMessage;

    [Tooltip("時計回りの基準（テーブル中央）")]
    [SerializeField] private Transform tableCenter;

    [SerializeField] private ListingDisplay listingDisplay;

    [Tooltip("購入・見送り・入札額を表示する")]
    [SerializeField] private PlayerLabelBoard bidBoard;

    [Tooltip("死亡したプレイヤーに × を表示する")]
    [SerializeField] private PlayerLabelBoard deathBoard;

    [Tooltip("オークション中に手札を隠すフェード")]
    [SerializeField] private CanvasGroupFader handFader;

    [SerializeField] private TurnUI ui;

    [Tooltip("ターンの結果を表示しておく秒数")]
    [SerializeField] private float resultSeconds = 2f;

    readonly Dictionary<int, IPlayerAgent> agents = new Dictionary<int, IPlayerAgent>();
    bool isActive;
    string turnHeader;
    readonly List<int> deathOrder = new List<int>();

    public void Activate(IEnumerable<IPlayerAgent> playerAgents)
    {
        agents.Clear();
        foreach (IPlayerAgent agent in playerAgents)
            agents[agent.PlayerNumber] = agent;
        isActive = true;
    }

    void Awake()
    {
        selector.SellerTurnStarted += HandleSellerTurnStarted;
        game.PlayerDied += HandlePlayerDied;
    }

    void OnDestroy()
    {
        selector.SellerTurnStarted -= HandleSellerTurnStarted;
        game.PlayerDied -= HandlePlayerDied;
    }

    void HandleSellerTurnStarted(int firstSeller)
    {
        StartCoroutine(isActive ? RunGame(firstSeller) : sellerPresenter.Present(firstSeller));
    }

    void HandlePlayerDied(PlayerState player)
    {
        deathOrder.Add(player.PlayerNumber);
        if (player.PlayerNumber != registry.LocalPlayerNumber)
            deathBoard.Show(player.PlayerNumber, "×");
        SetInfo($"{NameOf(player.PlayerNumber)} が脱落しました");
    }

    string NameOf(int player) => player == registry.LocalPlayerNumber ? "あなた" : $"Player{player}";

    /// <summary>進行状況の欄に「ラウンドと誰のターンか」と、今の様子を表示する</summary>
    void SetInfo(string detail)
    {
        ui.SetTurnInfo(string.IsNullOrEmpty(turnHeader) ? detail : $"{turnHeader}\n{detail}");
    }

    IEnumerator RunGame(int firstSeller)
    {
        var order = new TurnOrder(ClockwisePlayers());
        int seller = firstSeller;
        int round = 1;

        while (seller != PlayerRegistry.NoPlayer)
        {
            turnHeader = $"ラウンド {round}　{NameOf(seller)} のターン";
            SetInfo("出品者の番です");
            yield return sellerPresenter.Present(seller);
            yield return RunTurn(seller);
            order.MarkTurnDone(seller);

            if (game.AlivePlayers.Count() <= 1)
                break;

            if (order.IsRoundComplete(game.IsAlive))
            {
                turnHeader = $"ラウンド {round} 終了";
                SetInfo("ドローフェーズ（生きている全員が 1 枚ずつ引きます）");
                yield return announcer.Show(drawPhaseMessage);
                yield return drawPhase.DrawRound();
                order.StartNewRound();
                round++;
            }

            seller = order.NextAlive(seller, game.IsAlive);
        }

        ShowVictory();
    }

    IEnumerator RunTurn(int seller)
    {
        if (!game.TryGet(seller, out PlayerState sellerState))
            yield break;

        if (!sellerState.HasGoods)
        {
            game.ConsumePassRight(seller);
            yield return ShowResult($"{NameOf(seller)} は出品できるグッズがないため見送り権を使いました");
            yield break;
        }

        Listing listing = null;
        yield return RunListing(seller, result => listing = result);
        bool hidesHand = game.IsAlive(registry.LocalPlayerNumber);
        if (hidesHand)
            handFader.FadeOut(null);
        yield return listingDisplay.Present(listing);
        if (hidesHand)
            handFader.FadeIn(null);

        List<int> buyers = null;
        yield return RunPurchase(listing, result => buyers = result);

        if (buyers.Count == 0)
        {
            game.ApplyBuffs(seller, listing.Buffs);
            yield return ShowResult($"誰も購入しませんでした（{listing.Goods.DisplayName} は消費）");
        }
        else if (buyers.Count == 1)
        {
            yield return Settle(listing, buyers[0], listing.MinPrice);
        }
        else
        {
            AuctionModel auction = null;
            yield return RunAuction(listing, buyers, result => auction = result);
            if (auction.Winner == PlayerRegistry.NoPlayer)
            {
                game.ApplyBuffs(seller, listing.Buffs);
                yield return ShowResult($"落札者がいませんでした（{listing.Goods.DisplayName} は消費）");
            }
            else
            {
                yield return Settle(listing, auction.Winner, auction.Price);
            }
        }

        listingDisplay.Hide();
    }

    IEnumerator RunListing(int seller, System.Action<Listing> onListed)
    {
        var session = new ListingSession(game, seller);
        SetInfo($"{NameOf(seller)} が出品する商品を選んでいます");
        IPlayerAgent agent = agents[seller];
        agent.BeginListing(session);
        yield return new WaitUntil(() => session.IsConfirmed);
        agent.EndPhase();
        onListed(session.ToListing());
    }

    IEnumerator RunPurchase(Listing listing, System.Action<List<int>> onDecided)
    {
        int[] candidates = game.AlivePlayers.Select(player => player.PlayerNumber).Where(player => player != listing.Seller).ToArray();
        var session = new PurchaseSession(game, listing, candidates);
        int decided = 0;
        string ShowWaiting() => $"出品：{listing.Goods.DisplayName}（最低 {listing.MinPrice}）　購入か見送りを選んでいます {decided}/{candidates.Length}";
        SetInfo(ShowWaiting());
        session.Decided += (player, buy) =>
        {
            decided++;
            bidBoard.Show(player, buy ? "購入" : "見送り");
            SetInfo(ShowWaiting());
            if (!buy)
                game.ConsumePassRight(player);
        };

        foreach (int candidate in candidates)
            agents[candidate].BeginPurchase(session);
        yield return new WaitUntil(() => candidates.All(candidate => session.HasDecided(candidate) || !game.IsAlive(candidate)));
        foreach (int candidate in candidates)
            agents[candidate].EndPhase();

        onDecided(session.Buyers.Where(game.IsAlive).ToList());
    }

    IEnumerator RunAuction(Listing listing, List<int> buyers, System.Action<AuctionModel> onFinished)
    {
        var auction = new AuctionModel(listing, buyers, player => game.TryGet(player, out PlayerState state) ? state.Coins : 0);
        int leader = PlayerRegistry.NoPlayer;
        void ShowAuction()
        {
            string highest = leader == PlayerRegistry.NoPlayer ? "入札なし" : $"最高額 {auction.HighestBid}（{NameOf(leader)}）";
            SetInfo($"オークション中：{listing.Goods.DisplayName}（最低 {listing.MinPrice}）　{highest}　残り {auction.ActivePlayers.Count()} 人");
        }
        ShowAuction();
        auction.BidPlaced += (player, amount) =>
        {
            leader = player;
            bidBoard.Show(player, amount.ToString());
            ShowAuction();
        };
        auction.Folded += player =>
        {
            bidBoard.Show(player, "降りる");
            ShowAuction();
            game.ConsumePassRight(player);
        };

        bool hidesHand = game.IsAlive(registry.LocalPlayerNumber);
        if (hidesHand)
            handFader.FadeOut(null);

        IPlayerAgent[] watchers = game.AlivePlayers.Select(player => agents[player.PlayerNumber]).ToArray();
        foreach (IPlayerAgent agent in watchers)
            agent.BeginAuction(auction);
        yield return new WaitUntil(() =>
        {
            foreach (int player in auction.ActivePlayers.Where(player => !game.IsAlive(player)).ToArray())
                auction.TryFold(player);
            return auction.IsFinished;
        });
        foreach (IPlayerAgent agent in watchers)
            agent.EndPhase();

        if (hidesHand)
            handFader.FadeIn(null);
        onFinished(auction);
    }

    IEnumerator Settle(Listing listing, int buyer, int price)
    {
        game.SettlePurchase(buyer, listing.Seller, listing.Goods, price);
        game.ApplyBuffs(buyer, listing.Buffs);
        yield return ShowResult($"{NameOf(buyer)} が {listing.Goods.DisplayName} を {price} コインで購入しました");
    }

    IEnumerator ShowResult(string message)
    {
        SetInfo(message);
        yield return new WaitForSeconds(resultSeconds);
    }

    void ShowVictory()
    {
        PlayerState winner = game.AlivePlayers.FirstOrDefault();
        string winnerText = winner == null ? "勝者なし" : $"勝者 {NameOf(winner.PlayerNumber)}";
        turnHeader = string.Empty;
        SetInfo($"ゲーム終了　{winnerText}");

        ui.ShowVictory("GAME SET", winnerText, string.Join("\n", Standings()));
        Debug.Log($"[Game] ゲーム終了。勝者 Player{winner?.PlayerNumber}");
    }

    /// <summary>最後まで生き残った人が 1 位、以降は後に脱落した人ほど上位にした順位表。</summary>
    IEnumerable<string> Standings()
    {
        IEnumerable<PlayerState> ranked = game.AlivePlayers
            .Concat(Enumerable.Reverse(deathOrder).Select(player => game.TryGet(player, out PlayerState state) ? state : null).Where(state => state != null));

        return ranked.Select((player, index) =>
        {
            string state = player.IsAlive ? "生存" : $"{deathOrder.IndexOf(player.PlayerNumber) + 1}番目に脱落";
            return $"{index + 1}位　{NameOf(player.PlayerNumber)}　{state}　（コイン {player.Coins}　落札 {player.Collection.Count}）";
        });
    }

    /// <summary>上から見て時計回り（テーブル中央から見た向きの角度が小さい順）に並べたプレイヤー番号。</summary>
    IEnumerable<int> ClockwisePlayers()
    {
        Vector3 center = tableCenter.position;
        return registry.PlayerNumbers
            .Where(player => registry.TryGet(player, out _))
            .OrderBy(player =>
            {
                registry.TryGet(player, out Transform t);
                Vector3 d = t.position - center;
                return Mathf.Atan2(d.x, d.z);
            });
    }
}
