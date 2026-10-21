using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 出品者のターンを順に進める。
/// 1 ターン：ターンの告知（FIRST TURN / NEXT TURN）→ 出品者の演出 → 出品（グッズがなければ見送り権を消費）
/// →（スペシャルカード付きならスペシャルカードを明かす）→（真贋証明が付いていれば付いている効果カードを公開）
/// → 出品者以外の全員でオークション（入札か降りるか）
/// → 落札者がいれば入札額で購入し（最後の 1v1 では代金は出品者に入らない）、効果カードとスペシャルカードは落札者に、全員が降りたら出品者に発動する
/// （所持コインより多く入札できるが、落札額を払えなければ購入は成立せず見送り権を失い（足りなければ脱落）、全員が降りたときと同じく出品者に発動する）
/// （付いていた効果カード・スペシャルカード・ランダムで選ばれたスペシャルカードは、ディゾルブで公開してから発動する）。
/// 出品には横やり（効果カードを付けられない）と投げ売り（最低出品額の値引き）をかけ、出品が確定したときに使う。
/// ターン中に脱落した人がいれば、ターンの後に名前入りの DROP OUT を告知する（脱落で勝敗が決まったら、残りの精算や効果の公開は飛ばしてすぐに告知する）。
/// 出品者が出品を決める前にリタイアしたら、そのターンは打ち切る（脱落した出品者のターンは行わない）。
/// 2度目を受けた人がいれば、そのターンの後に追加の出品ターンを行う（1 周の数には入れない。出品できるグッズがなければ飛ばす）。
/// 生きている全員が 1 回ずつ出品者を終えたらドローし、今の向き（最初は時計回り、逆転で反対）で次の生きているプレイヤーへ。
/// 最後の 1 人が勝者で、名前入りの WIN を告知してから結果を表示する。
/// 操作役（IPlayerAgent）を Activate で渡されるまでは、最初の出品者の演出だけを行う。
/// 出品・オークションの待ち時間切れは、Timeouts を通して操作と同じ順に反映する。
/// </summary>
public class TurnFlowController : MonoBehaviour
{
    [SerializeField] private SellerSelector selector;

    [SerializeField] private GameManager game;

    [SerializeField] private PlayerRegistry registry;

    [Tooltip("進行状況・告知・結果に出すプレイヤーの名前")]
    [SerializeField] private PlayerNameDirectory names;

    [SerializeField] private SellerTurnPresenter sellerPresenter;

    [Tooltip("出品者のターンの間、出品者の縁を光らせる")]
    [SerializeField] private SellerHighlighter sellerHighlight;

    [SerializeField] private DrawPhaseController drawPhase;

    [SerializeField] private GameStartAnnouncer announcer;

    [Tooltip("1 周ごとのドローの前に出す告知の文字")]
    [SerializeField] private GameObject drawPhaseMessage;

    [Tooltip("最初のターンの出品者の演出の前に出す告知の文字")]
    [SerializeField] private GameObject firstTurnMessage;

    [Tooltip("2 ターン目以降の出品者の演出の前に出す告知の文字")]
    [SerializeField] private GameObject nextTurnMessage;

    [Tooltip("勝者が決まったら、勝者の名前を入れて出す告知")]
    [SerializeField] private PlayerNameMessage winMessage;

    [Tooltip("脱落したプレイヤーが出たら、そのターンの後に名前を入れて出す告知")]
    [SerializeField] private PlayerNameMessage dropOutMessage;

    [Tooltip("自分以外が脱落したら、告知の前にその人の正面へカメラを動かす")]
    [SerializeField] private PlayerFrontCamera frontCamera;

    [Tooltip("脱落した人の頭からカメラまでの距離（プレイヤーの大きさ倍）")]
    [SerializeField] private float dropOutShotDistance = 1.4f;

    [Tooltip("脱落した人の頭から見たカメラの高さ（プレイヤーの大きさ倍）")]
    [SerializeField] private float dropOutShotHeight = 0.05f;

    [Tooltip("時計回りの基準（テーブル中央）")]
    [SerializeField] private Transform tableCenter;

    [SerializeField] private ListingDisplay listingDisplay;

    [Tooltip("スペシャルカード付きの商品が出品されたら、オークションの前にスペシャルカードを明かす")]
    [SerializeField] private SpecialCardReveal specialReveal;

    [Tooltip("明かしたスペシャルカードを、出品が終わるまで一覧で光らせる")]
    [SerializeField] private SpecialCardCatalog specialCatalog;

    [Tooltip("鑑定・全鑑定・真贋証明・落札時の付いていた効果カード・ランダムで選ばれたスペシャルカードを公開する演出")]
    [SerializeField] private EffectCardReveal effectReveal;

    [Tooltip("ランダムで選ばれたスペシャルカードのデータを探す山札")]
    [SerializeField] private SpecialCardDeck specialDeck;

    [Tooltip("オークションで降りたことを表示する")]
    [SerializeField] private PlayerLabelBoard bidBoard;

    [Tooltip("オークションで入札したプレイヤーに、入札額の札を掲げさせる")]
    [SerializeField] private BidPaddleBoard bidPaddles;

    [Tooltip("他のプレイヤーが入札したら、その人を正面からアップで映す")]
    [SerializeField] private BidderCloseUpShot bidderShot;

    [Tooltip("落札した人の手元にカードを置き、所持コインのリールを巻く演出")]
    [SerializeField] private WinnerPresenter winnerPresenter;

    [Tooltip("死亡したプレイヤーに × を表示する")]
    [SerializeField] private PlayerLabelBoard deathBoard;

    [Tooltip("オークション中に手札を隠すフェード")]
    [SerializeField] private CanvasGroupFader handFader;

    [Tooltip("頭のモニターに出す残りの見送り権の枚数。オークション中は隠す")]
    [SerializeField] private MonitorPassRightBoard monitorPassRights;

    [SerializeField] private TurnUI ui;

    [Tooltip("ターンの結果を表示しておく秒数")]
    [SerializeField] private float resultSeconds = 2f;

    [Tooltip("オークションで所持コインより多く入札できる額")]
    [SerializeField] private int bidOverdraft = 10;

    [Tooltip("落札額を払えなかったときに失う見送り権の枚数（足りなければ脱落）")]
    [SerializeField] private int shortfallPenalty = 2;

    readonly Dictionary<int, IPlayerAgent> agents = new Dictionary<int, IPlayerAgent>();
    bool isActive;
    string turnHeader;
    readonly List<int> deathOrder = new List<int>();
    readonly Queue<int> pendingDropOuts = new Queue<int>();
    CardEffectResolver effectsInstance;
    PlayerActionDispatcher actionsInstance;
    TurnOrder order;
    ListingSession activeListing;
    AuctionModel activeAuction;

    /// <summary>他のコンポーネントの Awake から Activate されても使えるよう、最初に使うときに作る</summary>
    CardEffectResolver Effects => effectsInstance ??= new CardEffectResolver(game, NameOf, SpecialOf);

    PlayerActionDispatcher Actions => actionsInstance ??= new PlayerActionDispatcher(game);

    /// <summary>操作役が今の手続きへ操作を送る窓口</summary>
    public IPlayerCommands Commands => Actions;

    /// <summary>部屋主が今の手続きを待ち時間切れにする窓口</summary>
    public IPhaseTimeouts Timeouts => Actions;

    /// <summary>出品者が出品する商品を選び始めた</summary>
    public event System.Action<ListingSession> ListingStarted;

    /// <summary>出品の選択が終わった（確定したか、出品者が脱落した）</summary>
    public event System.Action ListingFinished;

    /// <summary>勝者が決まり、リザルトを出した</summary>
    public event System.Action GameFinished;

    /// <summary>周回が始まった（最初は 1。生きている全員が 1 回ずつ出品者を終えるごとに増える）</summary>
    public event System.Action<int> RoundStarted;

    /// <summary>出品者のターン（追加ターンを含む）が始まった</summary>
    public event System.Action<int> TurnStarted;

    /// <summary>オークションが始まった（参加者が降りたことはオークションの Folded で分かる）</summary>
    public event System.Action<AuctionModel> AuctionStarted;

    public event System.Action AuctionFinished;

    /// <summary>
    /// 操作役を渡して進行を始める。
    /// ネットワークでは、操作を全員へ配る経路と、全員で同じカード・効果になるよう山札と効果の乱数をそろえるシードも渡す。
    /// </summary>
    public void Activate(IEnumerable<IPlayerAgent> playerAgents, IPlayerActionChannel actionChannel = null, int? seed = null)
    {
        agents.Clear();
        foreach (IPlayerAgent agent in playerAgents)
            agents[agent.PlayerNumber] = agent;
        Actions.Connect(actionChannel);
        if (seed.HasValue)
        {
            drawPhase.Reseed(seed.Value);
            Effects.Reseed(seed.Value);
        }
        isActive = true;
    }

    /// <summary>全員の画面へ配られた操作を受け取る</summary>
    public void ReceiveAction(PlayerAction action) => Actions.Receive(action);

    /// <summary>
    /// プレイヤーの操作役を差し替える（切断した人を CPU に代えるなど）。
    /// その人が今の出品・オークションの途中なら、新しい操作役が続きを引き継ぐ。
    /// </summary>
    public void ReplaceAgent(IPlayerAgent agent)
    {
        if (agents.TryGetValue(agent.PlayerNumber, out IPlayerAgent previous))
            previous.EndPhase();
        agents[agent.PlayerNumber] = agent;

        if (activeListing != null && activeListing.Seller == agent.PlayerNumber)
            agent.BeginListing(activeListing);
        else if (activeAuction != null)
            agent.BeginAuction(activeAuction);
    }

    /// <summary>切断した人の代わりに CPU が操作することを知らせる</summary>
    public void AnnounceCpuTakeover(int player)
    {
        SetInfo($"{NameOf(player)} の接続が切れたため、CPU が代わりに操作します");
    }

    void Awake()
    {
        selector.SellerTurnStarted += HandleSellerTurnStarted;
        game.PlayerDied += HandlePlayerDied;
        drawPhase.GoodsDrawBlocked += HandleGoodsDrawBlocked;
    }

    void OnDestroy()
    {
        selector.SellerTurnStarted -= HandleSellerTurnStarted;
        game.PlayerDied -= HandlePlayerDied;
        drawPhase.GoodsDrawBlocked -= HandleGoodsDrawBlocked;
    }

    CardData SpecialOf(CardEffect effect) => specialDeck.Specials.FirstOrDefault(card => card != null && card.Effect == effect);

    void HandleGoodsDrawBlocked(int player)
    {
        SetInfo($"{NameOf(player)} は圧力で出品カードを引けませんでした");
    }

    void HandleSellerTurnStarted(int firstSeller)
    {
        StartCoroutine(isActive ? RunGame(firstSeller) : PresentSeller(firstSeller, true));
    }

    void HandlePlayerDied(PlayerState player)
    {
        deathOrder.Add(player.PlayerNumber);
        pendingDropOuts.Enqueue(player.PlayerNumber);
        if (player.PlayerNumber != registry.LocalPlayerNumber)
            deathBoard.Pin(player.PlayerNumber, "×");
        SetInfo($"{NameOf(player.PlayerNumber)} が脱落しました");
    }

    /// <summary>入力された名前（なければ「Player + 番号」）。自分も名前で表す</summary>
    string NameOf(int player) => names.NameOf(player);

    /// <summary>進行状況の欄に「ラウンドと誰のターンか」と、今の様子を表示する</summary>
    void SetInfo(string detail)
    {
        ui.SetTurnInfo(string.IsNullOrEmpty(turnHeader) ? detail : $"{turnHeader}\n{detail}");
    }

    IEnumerator RunGame(int firstSeller)
    {
        order = new TurnOrder(ClockwisePlayers());
        int seller = firstSeller;
        int round = 1;
        bool isFirstTurn = true;
        RoundStarted?.Invoke(round);

        while (seller != PlayerRegistry.NoPlayer)
        {
            turnHeader = $"ラウンド {round}　{NameOf(seller)} のターン";
            SetInfo("出品者の番です");
            TurnStarted?.Invoke(seller);
            sellerHighlight.Show(seller);
            yield return PresentSeller(seller, isFirstTurn);
            isFirstTurn = false;
            yield return RunTurn(seller);
            sellerHighlight.Hide();
            order.MarkTurnDone(seller);
            yield return AnnounceDropOuts();
            yield return RunExtraTurns(round);

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
                RoundStarted?.Invoke(round);
            }

            seller = order.NextAlive(seller, game.IsAlive);
        }

        PlayerState winner = game.AlivePlayers.FirstOrDefault();
        if (winner != null)
            yield return AnnounceWithName(winMessage, winner.PlayerNumber);
        ShowVictory();
    }

    /// <summary>ターンの告知（最初は FIRST TURN、以降は NEXT TURN）を出してから、出品者の演出を行う</summary>
    IEnumerator PresentSeller(int seller, bool isFirstTurn)
    {
        yield return announcer.Show(isFirstTurn ? firstTurnMessage : nextTurnMessage);
        yield return sellerPresenter.Present(seller);
    }

    /// <summary>
    /// このターンに脱落したプレイヤーを、1 人ずつ名前を入れて告知する。
    /// 自分以外はその人の正面へカメラを動かしてから告知し、全員の告知が終わったら元の視点へ戻す
    /// </summary>
    IEnumerator AnnounceDropOuts()
    {
        while (pendingDropOuts.Count > 0)
        {
            int player = pendingDropOuts.Dequeue();
            if (player != registry.LocalPlayerNumber && registry.TryGet(player, out Transform body))
                yield return frontCamera.MoveToFront(body, () => PlayerBody.HeadPosition(body), dropOutShotDistance, dropOutShotHeight);
            yield return AnnounceWithName(dropOutMessage, player);
        }
        yield return frontCamera.Return();
    }

    /// <summary>
    /// 2度目を受けた人に、1 周の数に入れない追加の出品ターンを順に行う（勝者が決まったらやめる）。
    /// 出品できるグッズがない人は、見送り権を使わずに追加ターンを飛ばす。
    /// 2度目は追加ターンが終わる（飛ばす）まで残しておき、そこで 1 回使う。
    /// </summary>
    IEnumerator RunExtraTurns(int round)
    {
        while (game.AlivePlayers.Count() > 1 && TryFindExtraTurn(out int player))
        {
            turnHeader = $"ラウンド {round}　{NameOf(player)} の追加ターン（2度目）";
            if (!game.TryGet(player, out PlayerState state) || !state.HasGoods)
            {
                yield return ShowResult($"{NameOf(player)} は出品できるグッズがないため、追加ターンはありません");
                game.TryUseStatus(player, PlayerStatus.ExtraTurn);
                continue;
            }

            SetInfo("出品者の番です");
            TurnStarted?.Invoke(player);
            sellerHighlight.Show(player);
            yield return PresentSeller(player, false);
            yield return RunTurn(player);
            game.TryUseStatus(player, PlayerStatus.ExtraTurn);
            sellerHighlight.Hide();
            yield return AnnounceDropOuts();
        }
    }

    bool TryFindExtraTurn(out int player)
    {
        foreach (PlayerState state in game.AlivePlayers.ToArray())
        {
            if (state.StatusOf(PlayerStatus.ExtraTurn) > 0)
            {
                player = state.PlayerNumber;
                return true;
            }
        }
        player = PlayerRegistry.NoPlayer;
        return false;
    }

    IEnumerator AnnounceWithName(PlayerNameMessage message, int player)
    {
        message.SetName(NameOf(player));
        yield return announcer.Show(message.Message);
    }

    IEnumerator RunTurn(int seller)
    {
        if (!game.TryGet(seller, out PlayerState sellerState) || !sellerState.IsAlive)
            yield break;

        if (!sellerState.HasGoods)
        {
            game.ConsumePassRight(seller);
            if (!IsDecided)
                yield return ShowResult($"{NameOf(seller)} は出品できるグッズがないため見送り権を使いました");
            yield break;
        }

        Listing listing = null;
        yield return RunListing(seller, result => listing = result);
        if (listing == null)
            yield break;

        bool hidesHand = game.IsAlive(registry.LocalPlayerNumber);
        if (hidesHand)
            handFader.FadeOut(null);
        yield return listingDisplay.Present(listing);
        if (listing.Goods.Special != null)
            yield return specialReveal.Present(listing.Goods.Special);
        specialCatalog.SetAuctioned(listing.Goods.Special);
        yield return PresentEffects(Effects.ResolveListing(listing));
        if (hidesHand)
            handFader.FadeIn(null);

        List<int> bidders = game.AlivePlayers.Select(player => player.PlayerNumber).Where(player => player != seller).ToList();
        AuctionModel auction = null;
        yield return RunAuction(listing, bidders, result => auction = result);
        if (IsDecided)
        {
            EndListing();
            yield break;
        }

        int winner = auction.Winner;
        bool isShortfall = winner != PlayerRegistry.NoPlayer && !auction.CanAfford(winner, auction.Price);
        int recipient = winner == PlayerRegistry.NoPlayer || isShortfall ? seller : winner;
        if (winner == PlayerRegistry.NoPlayer)
        {
            game.ApplyBuffs(seller, listing.Buffs);
            yield return ShowResult($"落札者がいませんでした（{listing.Goods.DisplayName} は消費）");
        }
        else if (isShortfall)
        {
            yield return FailPurchase(listing, winner, auction.Price, auction.ShortfallPenalty);
        }
        else
        {
            yield return Settle(listing, winner, auction.Price);
        }
        if (!IsDecided)
            yield return PresentEffects(Effects.ResolveReceived(recipient, seller, listing.Buffs, listing.Goods.Special));
        EndListing();
    }

    /// <summary>生き残りが 1 人以下になり、勝敗が決まった（以降の精算や効果の公開は飛ばして脱落の告知へ進む）</summary>
    bool IsDecided => game.AlivePlayers.Count() <= 1;

    /// <summary>出品の表示（札・一覧の光・テーブルのカード）を片付ける</summary>
    void EndListing()
    {
        bidPaddles.LowerAll();
        specialCatalog.SetAuctioned(null);
        listingDisplay.Hide();
    }

    IEnumerator RunListing(int seller, System.Action<Listing> onListed)
    {
        bool interfered = StatusOf(seller, PlayerStatus.Interference) > 0;
        int discount = StatusOf(seller, PlayerStatus.FireSaleDiscount);
        var session = new ListingSession(game, seller, !interfered, discount);
        string notes = (interfered ? "（横やり：効果カードを付けられません）" : string.Empty)
            + (discount > 0 ? $"（投げ売り：最低出品額 -{discount}）" : string.Empty);
        SetInfo($"{NameOf(seller)} が出品する商品を選んでいます{notes}");
        activeListing = session;
        Actions.BeginListing(session);
        agents[seller].BeginListing(session);
        ListingStarted?.Invoke(session);
        yield return new WaitUntil(() => session.IsConfirmed || !game.IsAlive(seller));
        agents[seller].EndPhase();
        Actions.EndPhase();
        activeListing = null;
        ListingFinished?.Invoke();
        if (!session.IsConfirmed)
        {
            onListed(null);
            yield break;
        }

        if (interfered)
            game.TryUseStatus(seller, PlayerStatus.Interference);
        if (discount > 0)
            game.UseAllStatus(seller, PlayerStatus.FireSaleDiscount);
        onListed(session.ToListing());
    }

    int StatusOf(int player, PlayerStatus status) => game.TryGet(player, out PlayerState state) ? state.StatusOf(status) : 0;

    IEnumerator RunAuction(Listing listing, List<int> bidders, System.Action<AuctionModel> onFinished)
    {
        var auction = new AuctionModel(listing, bidders, CoinsOf, bidOverdraft, shortfallPenalty);
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
            if (player == registry.LocalPlayerNumber)
            {
                bidPaddles.Raise(player, amount);
            }
            else
            {
                bidPaddles.RaiseLater(player, amount);
                bidderShot.Show(player);
            }
            ShowAuction();
        };
        auction.Folded += player =>
        {
            bidPaddles.Lower(player);
            bidBoard.Show(player, "降りる");
            ShowAuction();
            game.ConsumePassRight(player);
        };

        bool hidesHand = game.IsAlive(registry.LocalPlayerNumber);
        if (hidesHand)
            handFader.FadeOut(null);
        monitorPassRights.SetVisible(false);

        int[] watchers = game.AlivePlayers.Select(player => player.PlayerNumber).ToArray();
        activeAuction = auction;
        AuctionStarted?.Invoke(auction);
        Actions.BeginAuction(auction);
        foreach (int player in watchers)
            agents[player].BeginAuction(auction);
        yield return new WaitUntil(() =>
        {
            foreach (int player in auction.ActivePlayers.Where(player => !game.IsAlive(player)).ToArray())
                auction.TryFold(player);
            return auction.IsFinished;
        });
        foreach (int player in watchers)
            agents[player].EndPhase();
        Actions.EndPhase();
        activeAuction = null;
        AuctionFinished?.Invoke();
        yield return new WaitWhile(() => bidderShot.IsPlaying);

        bidPaddles.LowerAllExcept(auction.Winner);
        if (auction.Winner != PlayerRegistry.NoPlayer)
            bidPaddles.Raise(auction.Winner, auction.Price);

        if (hidesHand)
            handFader.FadeIn(null);
        monitorPassRights.SetVisible(true);
        onFinished(auction);
    }

    IEnumerator Settle(Listing listing, int buyer, int price)
    {
        int coinsBefore = CoinsOf(buyer);
        bool sellerEarns = game.SellerEarnsProceeds;
        game.SettlePurchase(buyer, listing.Seller, listing.Goods, price);
        int coinsAfter = CoinsOf(buyer);
        game.ApplyBuffs(buyer, listing.Buffs);

        string proceeds = sellerEarns ? string.Empty : "（最後の 1v1 のため、代金は出品者に入りません）";
        SetInfo($"{NameOf(buyer)} が {listing.Goods.DisplayName} を {price} コインで購入しました{proceeds}");
        Sprite sprite = listing.Goods.Data != null ? listing.Goods.Data.Parameter.CardSprite : null;
        yield return winnerPresenter.Present(buyer, sprite, coinsBefore, coinsAfter);
    }

    /// <summary>落札額を払えなかった：購入は成立せず（商品は消費し、効果は出品者へ）、落札者は見送り権を失う（足りなければ脱落）</summary>
    IEnumerator FailPurchase(Listing listing, int winner, int price, int penalty)
    {
        game.ApplyBuffs(listing.Seller, listing.Buffs);
        game.ConsumePassRight(winner, penalty);
        if (IsDecided)
            yield break;

        string outcome = game.IsAlive(winner) ? $"見送り権を {penalty} 枚失いました" : "見送り権が足りず脱落しました";
        yield return ShowResult($"{NameOf(winner)} は {price} コインを払えず購入できませんでした。{outcome}（{listing.Goods.DisplayName} は消費）");
    }

    int CoinsOf(int player) => game.TryGet(player, out PlayerState state) ? state.Coins : 0;

    /// <summary>効果の結果を見せる：メッセージを出し、公開するカードを順に見せ、所持コインの変化をリールで巻き、追加のドローを配り、逆転を反映する</summary>
    IEnumerator PresentEffects(EffectReport report)
    {
        if (report.IsEmpty)
            yield break;

        for (int i = 0; i < report.Reversals; i++)
            order?.Reverse();
        if (report.Messages.Count > 0)
            SetInfo(string.Join("\n", report.Messages));
        foreach (EffectReport.Reveal reveal in report.Reveals)
            yield return effectReveal.Present(reveal.Title, reveal.Cards, reveal.From);
        foreach (EffectReport.CoinChange change in report.CoinChanges)
            yield return winnerPresenter.PresentCoins(change.Player, change.Before, change.After);
        foreach (EffectReport.Draw draw in report.Draws)
        {
            if (game.IsAlive(draw.Player))
                yield return drawPhase.DrawExtra(draw.Player, draw.Kind);
        }
        if (report.Messages.Count > 0)
            yield return new WaitForSeconds(resultSeconds);
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
        GameFinished?.Invoke();
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
