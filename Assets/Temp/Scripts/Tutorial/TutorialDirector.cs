using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// チュートリアルの進行。SinglePlay の試合を、決めたカード・決めた動きの CPU で進め、場面ごとに案内を出す。
/// ・最初の出品者は自分。CPU は見送り権 cpuPassRights 枚で始め、オークションでは案内を読み終えてから降りる。
/// ・ターン 1：出品の操作（手札を開く → カードの詳細 → 選ぶ → バフ → 付加価値 → デバフ → 確定）を 1 つずつ案内する。
///   オークションは CPU の 1 人（ライバル）が落札し、残りの CPU は降りて脱落する。
///   落札者・落札者なしのときのエフェクトカードの発動先、見送り権、インフォメーションを案内する。
/// ・ターン 2：ライバルの出品に自分が入札して落札する。
/// ・ターン 3：1 周後のドローで引いた最高レアリティのカードで、スペシャルカードを案内する。
///   そこからは制限を外して自由に操作させ、ライバルが降りて自分が勝ったらクリアを出す。
/// 案内中は、案内している操作だけを進行へ送る（TutorialCommandGate）。自分が脱落したらチュートリアルをやめる。
/// </summary>
public class TutorialDirector : MonoBehaviour
{
    [SerializeField] private TutorialNavigator navigator;

    [SerializeField] private TutorialDeck deck;

    [SerializeField] private SinglePlaySessionBootstrap singlePlay;

    [SerializeField] private GameStartGate startGate;

    [SerializeField] private GameManager game;

    [SerializeField] private PlayerRegistry registry;

    [SerializeField] private SellerSelector sellerSelector;

    [SerializeField] private TurnFlowController turnFlow;

    [SerializeField] private DrawPhaseController drawPhase;

    [SerializeField] private LocalPlayerAgent localAgent;

    [SerializeField] private LocalHandPresenter hand;

    [SerializeField] private CardHandView handView;

    [SerializeField] private PauseMenu pause;

    [Tooltip("CPU が最初に持っている見送り権")]
    [SerializeField] private int cpuPassRights = 0;

    [Header("案内で明るくする UI")]
    [SerializeField] private RectTransform startButton;

    [SerializeField] private RectTransform handArea;

    [SerializeField] private RectTransform raisePanel;

    [SerializeField] private RectTransform confirmButton;

    [SerializeField] private RectTransform amountReels;

    [SerializeField] private RectTransform bidButton;

    [Tooltip("カードの一覧を開くインフォメーションマーク")]
    [SerializeField] private RectTransform infoButton;

    [Tooltip("インフォメーションで開くカードの一覧（開いている間だけ有効）")]
    [SerializeField] private RectTransform catalogWindow;

    [Header("案内で使うカード（TutorialDeck で自分に配るもの）")]
    [Tooltip("ターン 1 で出品する出品カード")]
    [SerializeField] private CardData firstGoods;

    [SerializeField] private CardData buffCard;

    [SerializeField] private CardData debuffCard;

    [Tooltip("ターン 3 で出品する、スペシャルカード付きの出品カード")]
    [SerializeField] private CardData specialGoods;

    readonly Dictionary<int, TutorialCpuAgent> cpus = new Dictionary<int, TutorialCpuAgent>();
    TutorialCommandGate gate;
    ListingSession listing;
    AuctionModel auction;
    int finishedAuctions;
    bool gameFinished;
    bool bubbleClicked;
    bool cpusMayList;
    bool cpusMayAct;
    bool isReleased;
    bool isFinished;

    int Local => registry.LocalPlayerNumber;

    bool IsLocalListing => listing != null && listing.Seller == Local;

    void Start()
    {
        if (!singlePlay.IsActive)
        {
            navigator.Finish();
            enabled = false;
            return;
        }

        drawPhase.UseScriptedDraw(deck);
        sellerSelector.AllowLocalSelect(() => sellerSelector.NotifySellerDecided(Local));
        gate = new TutorialCommandGate(turnFlow.Commands);
        localAgent.Initialize(localAgent.PlayerNumber, gate);
        ReplaceCpuAgents();

        startGate.Started += SetCpuPassRights;
        turnFlow.ListingStarted += HandleListingStarted;
        turnFlow.ListingFinished += HandleListingFinished;
        turnFlow.AuctionStarted += HandleAuctionStarted;
        turnFlow.AuctionFinished += HandleAuctionFinished;
        turnFlow.GameFinished += HandleGameFinished;
        game.PlayerDied += HandlePlayerDied;
        navigator.BubbleClicked += HandleBubbleClicked;
        pause.Opened += HandlePauseOpened;
        pause.Closed += HandlePauseClosed;

        StartCoroutine(Run());
    }

    void OnDestroy()
    {
        startGate.Started -= SetCpuPassRights;
        turnFlow.ListingStarted -= HandleListingStarted;
        turnFlow.ListingFinished -= HandleListingFinished;
        turnFlow.AuctionStarted -= HandleAuctionStarted;
        turnFlow.AuctionFinished -= HandleAuctionFinished;
        turnFlow.GameFinished -= HandleGameFinished;
        game.PlayerDied -= HandlePlayerDied;
        navigator.BubbleClicked -= HandleBubbleClicked;
        pause.Opened -= HandlePauseOpened;
        pause.Closed -= HandlePauseClosed;
    }

    void ReplaceCpuAgents()
    {
        foreach (SinglePlayPlayer player in singlePlay.Players.Where(player => player.IsCpu))
        {
            var agent = player.gameObject.AddComponent<TutorialCpuAgent>();
            agent.Initialize(player.PlayerNumber, game, turnFlow.Commands, () => cpusMayList || isReleased, () => cpusMayAct || isReleased);
            turnFlow.ReplaceAgent(agent);
            cpus[player.PlayerNumber] = agent;
        }
    }

    /// <summary>プレイヤーの状態はスタート時に作られるため、作られた後に CPU の見送り権を合わせる</summary>
    void SetCpuPassRights()
    {
        foreach (SinglePlayPlayer player in singlePlay.Players.Where(player => player.IsCpu))
        {
            if (game.TryGet(player.PlayerNumber, out PlayerState state))
                game.AddPassRights(player.PlayerNumber, cpuPassRights - state.PassRights);
        }
    }

    void HandleListingStarted(ListingSession session) => listing = session;

    void HandleListingFinished() => listing = null;

    void HandleAuctionStarted(AuctionModel model) => auction = model;

    void HandleAuctionFinished()
    {
        auction = null;
        finishedAuctions++;
    }

    void HandleGameFinished() => gameFinished = true;

    void HandleBubbleClicked() => bubbleClicked = true;

    void HandlePauseOpened() => navigator.SetSuspended(true);

    void HandlePauseClosed() => navigator.SetSuspended(false);

    void HandlePlayerDied(PlayerState player)
    {
        if (player.PlayerNumber == Local)
            Finish();
    }

    IEnumerator Run()
    {
        yield return Show("tutorial.intro.welcome", TutorialNavLayout.Center, true, () => startGate.HasStarted, startButton);
        navigator.Hide();

        yield return RunFirstListing();
        yield return RunFirstAuction();
        yield return RunInformation();
        cpusMayList = true;
        yield return RunBidding();
        yield return RunSpecialListing();

        yield return new WaitUntil(() => gameFinished);
        yield return ShowAndWaitClick("tutorial.clear", TutorialNavLayout.Top, false);
        Finish();
    }

    /// <summary>ターン 1：出品の操作を 1 つずつ案内する</summary>
    IEnumerator RunFirstListing()
    {
        yield return new WaitUntil(() => IsLocalListing);
        ListingSession session = listing;
        HandCard goods = FindInHand(firstGoods);
        HandCard buff = FindInHand(buffCard);
        HandCard debuff = FindInHand(debuffCard);
        if (goods == null || buff == null || debuff == null)
        {
            Abort("ターン 1 で案内するカードが手札にありません");
            yield break;
        }

        TutorialCpuAgent rival = cpus.OrderBy(entry => entry.Key).Select(entry => entry.Value).FirstOrDefault();
        if (rival != null)
            rival.BidInNextAuction();

        handView.KeepsSpread = true;
        yield return Show("tutorial.seller.open_hand", TutorialNavLayout.Top, true, () => hand.IsSpread, handArea);
        yield return Show("tutorial.seller.look_goods", TutorialNavLayout.Top, true, () => IsHovered(goods), ImageOf(goods));
        yield return ShowAndWaitClick("tutorial.seller.card_detail", TutorialNavLayout.Top, true, ImageOf(goods));

        gate.AllowSelectGoods(goods);
        yield return Show("tutorial.seller.select_goods", TutorialNavLayout.Top, true, () => session.Goods == goods, ImageOf(goods));
        gate.AllowAttachBuff(buff);
        yield return Show("tutorial.seller.attach_buff", TutorialNavLayout.Top, true, () => session.Buffs.Contains(buff), ImageOf(buff));
        gate.AllowRaise();
        yield return Show("tutorial.seller.raise", TutorialNavLayout.Top, true, () => session.IsRaised, raisePanel);
        gate.AllowAttachBuff(debuff);
        yield return Show("tutorial.seller.attach_debuff", TutorialNavLayout.Top, true, () => session.Buffs.Contains(debuff), ImageOf(debuff));
        gate.AllowConfirm();
        yield return Show("tutorial.seller.confirm", TutorialNavLayout.Top, true, () => session.IsConfirmed, confirmButton);
        EndGuidedListing();
    }

    /// <summary>ターン 1 のオークション：ライバルが落札し、付けたエフェクトカードが落札者に発動する。残りの CPU は降りて脱落する</summary>
    IEnumerator RunFirstAuction()
    {
        yield return new WaitUntil(() => auction != null);
        int finished = finishedAuctions;
        yield return ShowAndWaitClick("tutorial.auction.intro", TutorialNavLayout.Top, false);
        yield return LetCpusAct(finished);
        yield return ShowAndWaitClick("tutorial.auction.sold", TutorialNavLayout.Top, false);
        yield return ShowAndWaitClick("tutorial.auction.unsold", TutorialNavLayout.Top, false);
        yield return ShowAndWaitClick("tutorial.auction.fold", TutorialNavLayout.Top, false);
    }

    /// <summary>インフォメーションマークでカードの一覧を開き、閉じるまでを案内する</summary>
    IEnumerator RunInformation()
    {
        yield return Show("tutorial.info.open", TutorialNavLayout.Top, true, () => catalogWindow.gameObject.activeInHierarchy, infoButton);
        yield return Show("tutorial.info.catalog", TutorialNavLayout.Top, false, () => !catalogWindow.gameObject.activeInHierarchy, catalogWindow);
        navigator.Hide();
    }

    /// <summary>ターン 2：ライバルの出品に入札して落札する</summary>
    IEnumerator RunBidding()
    {
        yield return new WaitUntil(() => auction != null && auction.Participants.Contains(Local));
        AuctionModel model = auction;
        int finished = finishedAuctions;
        gate.AllowBid();
        yield return Show("tutorial.bid.intro", TutorialNavLayout.Top, true, () => model.BidOf(Local) > 0, TutorialPointerSide.Left, bidButton, amountReels);
        gate.Lock();
        yield return LetCpusAct(finished);
    }

    /// <summary>ターン 3：スペシャルカード付きのカードを案内し、そこからは自由に操作させる</summary>
    IEnumerator RunSpecialListing()
    {
        yield return new WaitUntil(() => IsLocalListing);
        HandCard goods = FindInHand(specialGoods);
        if (goods == null)
        {
            Abort("スペシャルカードの案内に使うカードが手札にありません");
            yield break;
        }

        handView.KeepsSpread = true;
        yield return Show("tutorial.special.open_hand", TutorialNavLayout.Top, true, () => hand.IsSpread, handArea);
        yield return Show("tutorial.special.look", TutorialNavLayout.Top, true, () => IsHovered(goods), ImageOf(goods));
        yield return Show("tutorial.special.hover", TutorialNavLayout.Top, true, () => IsOverSpecial(goods), SpecialOf(goods), ImageOf(goods));
        yield return ShowAndWaitClick("tutorial.special.detail", TutorialNavLayout.Top, true, SpecialOf(goods), ImageOf(goods));
        yield return ShowAndWaitClick("tutorial.free", TutorialNavLayout.Top, false);
        ReleaseControl();
    }

    IEnumerator LetCpusAct(int finishedBefore)
    {
        navigator.Hide();
        cpusMayAct = true;
        yield return new WaitUntil(() => finishedAuctions > finishedBefore);
        cpusMayAct = false;
    }

    IEnumerator Show(string key, TutorialNavLayout layout, bool dimmed, Func<bool> until, params RectTransform[] highlights)
    {
        return Show(key, layout, dimmed, until, TutorialPointerSide.Auto, highlights);
    }

    IEnumerator Show(string key, TutorialNavLayout layout, bool dimmed, Func<bool> until, TutorialPointerSide pointerSide, params RectTransform[] highlights)
    {
        navigator.Show(key, layout, dimmed, false, pointerSide, highlights);
        yield return new WaitUntil(until);
    }

    IEnumerator ShowAndWaitClick(string key, TutorialNavLayout layout, bool dimmed, params RectTransform[] highlights)
    {
        bubbleClicked = false;
        navigator.Show(key, layout, dimmed, true, highlights);
        yield return new WaitUntil(() => bubbleClicked);
        navigator.Hide();
    }

    void EndGuidedListing()
    {
        gate.Lock();
        handView.KeepsSpread = false;
        hand.Collapse();
        navigator.Hide();
    }

    void Abort(string reason)
    {
        Debug.LogWarning($"[Tutorial] {reason}。チュートリアルの案内を終えます");
        Finish();
    }

    /// <summary>操作の制限を外して自由に操作させる。CPU は止めずに動かす。クリアの案内のために案内の画面は残す</summary>
    void ReleaseControl()
    {
        isReleased = true;
        localAgent.Initialize(localAgent.PlayerNumber, turnFlow.Commands);
        handView.KeepsSpread = false;
        navigator.Release();
    }

    /// <summary>案内をやめ、操作の制限を外す</summary>
    void Finish()
    {
        if (isFinished)
            return;

        isFinished = true;
        StopAllCoroutines();
        ReleaseControl();
        navigator.Finish();
    }

    HandCard FindInHand(CardData data)
    {
        return game.TryGet(Local, out PlayerState me) ? me.Hand.FirstOrDefault(card => card.Data == data) : null;
    }

    RectTransform ImageOf(HandCard card)
    {
        return hand.TryGetImage(card, out Image image) ? image.rectTransform : null;
    }

    CardInfoOverlay OverlayOf(HandCard card)
    {
        return hand.TryGetImage(card, out Image image) ? image.GetComponentInChildren<CardInfoOverlay>(true) : null;
    }

    RectTransform SpecialOf(HandCard card)
    {
        CardInfoOverlay overlay = OverlayOf(card);
        return overlay != null ? overlay.SpecialRect : null;
    }

    bool IsHovered(HandCard card)
    {
        CardInfoOverlay overlay = OverlayOf(card);
        return overlay != null && overlay.IsVisible;
    }

    bool IsOverSpecial(HandCard card)
    {
        CardInfoOverlay overlay = OverlayOf(card);
        Mouse mouse = Mouse.current;
        return overlay != null && mouse != null && overlay.SpecialContains(mouse.position.ReadValue(), null);
    }
}
