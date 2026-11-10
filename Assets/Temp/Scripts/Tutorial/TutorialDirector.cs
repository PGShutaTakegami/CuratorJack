using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// チュートリアルの進行。SinglePlay の試合を、決めたカード・決めた動きの CPU で進め、場面ごとに案内を出す。
/// 席はランダムなので、CPU の役は自分から時計回りの順（A → B → C。ターン 2〜4 の出品者）で決める。B をライバルにする。
/// ・最初の出品者は自分。CPU は見送り権 cpuPassRights 枚で始め、オークションでは案内を読み終えてから降りる。
/// ・ターン 1：出品の操作（手札を開く → カードの詳細 → 選ぶ → バフ → 付加価値 → デバフ → 確定）を 1 つずつ案内する。
///   オークションはライバルが落札し、残りの CPU は降りて見送り権を使う。
///   落札者・落札者なしのときのエフェクトカードの発動先、見送り権、インフォメーションを案内する。
/// ・ターン 2（A の出品）：虫眼鏡で手元（見送り権のチケット・落札したカード）を見る → 自分が入札する → 最高額の間は降りられない
///   → ライバルと raiseRounds 回競り合って値段を上げる → ライバルが上回ったら降りる、を案内する。C は降りて脱落し、ライバルが落札する。
/// ・ターン 3（ライバルの出品。ライバルは虫眼鏡で見せる効果カードを付ける）：練習用のアイテムを 3 つ渡し、
///   虫眼鏡 → 電気プラグ → リモコンの順に 1 つずつ使わせてから、自分が入札して落札する。A は降りて脱落し、FINAL BATTLE になる。
/// ・ターン 4：1 周後のドローの後、FINAL BATTLE で変わるルールを案内してから、引いた最高レアリティのカードでスペシャルカードを案内する。
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

    [SerializeField] private TableInspectButtons inspectButtons;

    [SerializeField] private TableInspectCamera inspectCamera;

    [Tooltip("CPU が最初に持っている見送り権")]
    [SerializeField] private int cpuPassRights = 1;

    [Header("案内で明るくする UI")]
    [SerializeField] private RectTransform startButton;

    [SerializeField] private RectTransform handArea;

    [SerializeField] private RectTransform raisePanel;

    [SerializeField] private RectTransform confirmButton;

    [SerializeField] private RectTransform amountReels;

    [SerializeField] private RectTransform bidButton;

    [SerializeField] private RectTransform foldButton;

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

    [Tooltip("ターン 2 で降りる前に、ライバルと競り合って入札し返す回数")]
    [SerializeField, Min(0)] private int raiseRounds = 2;

    [Header("アイテムの案内（ターン 3 のオークション）")]
    [Tooltip("練習用に自分へ渡すアイテム（虫眼鏡・電気プラグ・リモコン）")]
    [SerializeField] private ItemDefinition[] practiceItems;

    [Tooltip("ターン 3 でライバルが出品に付ける効果カード（虫眼鏡で見せる）")]
    [SerializeField] private CardData rivalBuffCard;

    [Tooltip("アイテムのマスを並べた範囲")]
    [SerializeField] private RectTransform itemSlotsArea;

    [Tooltip("アイテムのマス（左から順に）")]
    [SerializeField] private RectTransform[] itemSlots;

    [Tooltip("リモコンの相手を選ぶ画面")]
    [SerializeField] private ItemTargetPicker targetPicker;

    [Tooltip("虫眼鏡で効果カードを見せる演出（見せ終わるまで次の案内を待つ）")]
    [SerializeField] private EffectCardReveal effectReveal;

    [Tooltip("虫眼鏡を使ってから効果カードが出始めるまで待つ上限（秒。使ったアイテムへカメラを向ける演出の後に出る）")]
    [SerializeField] private float revealStartTimeout = 4f;

    [Tooltip("手元に置いたアイテムのモデル。練習用のアイテムを配ったときにカメラを向けて見せる")]
    [SerializeField] private ItemTableDisplay itemTable;

    readonly Dictionary<int, TutorialCpuAgent> cpus = new Dictionary<int, TutorialCpuAgent>();
    TutorialCommandGate gate;
    TutorialCpuAgent rival;
    ListingSession listing;
    AuctionModel auction;
    int finishedAuctions;
    bool gameFinished;
    bool bubbleClicked;
    bool cpusMayList;
    bool cpusMayAct;
    bool isReleased;
    bool isExplaining;
    bool hasExplainedFinalBattle;
    bool isFinished;

    /// <summary>自分が最後の 1 人になってチュートリアルをクリアした（クリアの案内を出す直前）</summary>
    public event Action Cleared;

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
            agent.Initialize(player.PlayerNumber, game, turnFlow.Commands, () => (cpusMayList || isReleased) && !isExplaining, () => (cpusMayAct || isReleased) && !isExplaining);
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
        yield return RunFolding();
        yield return RunBidding();
        yield return RunSpecialListing();

        yield return new WaitUntil(() => hasExplainedFinalBattle || game.IsFinalDuel || gameFinished);
        if (!hasExplainedFinalBattle && !gameFinished)
        {
            yield return RunFinalBattleRules();
            navigator.Release();
        }

        yield return new WaitUntil(() => gameFinished);
        Cleared?.Invoke();
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

        rival = CpuClockwiseFromLocal(2);
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

    /// <summary>ターン 1 のオークション：ライバルが落札し、付けたエフェクトカードが落札者に発動する。残りの CPU は降りて見送り権を使う</summary>
    IEnumerator RunFirstAuction()
    {
        yield return new WaitUntil(() => auction != null);
        int finished = finishedAuctions;
        yield return ShowAndWaitClick("tutorial.auction.intro", TutorialNavLayout.Top, false);
        yield return LetCpusAct(finished);
        yield return ShowAndWaitClick("tutorial.auction.sold", TutorialNavLayout.Top, false);
        yield return ShowAndWaitClick("tutorial.auction.unsold", TutorialNavLayout.Top, false);
        yield return ShowAndWaitClick("tutorial.auction.fold", TutorialNavLayout.Top, false);
        if (rival != null)
        {
            rival.BidInNextAuction(raiseRounds + 1);
            rival.AttachInNextListing(rivalBuffCard);
        }
    }

    /// <summary>インフォメーションマークでカードの一覧を開き、閉じるまでを案内する</summary>
    IEnumerator RunInformation()
    {
        yield return Show("tutorial.info.open", TutorialNavLayout.Top, true, () => catalogWindow.gameObject.activeInHierarchy, infoButton);
        yield return Show("tutorial.info.catalog", TutorialNavLayout.Top, false, () => !catalogWindow.gameObject.activeInHierarchy, catalogWindow);
        navigator.Hide();
    }

    /// <summary>
    /// ターン 2：虫眼鏡で手元を見せてから入札させ、最高額の間は降りられないことを案内する。
    /// ライバルと raiseRounds 回競り合って値段を上げてから、上回られたところで降りさせる
    /// </summary>
    IEnumerator RunFolding()
    {
        yield return new WaitUntil(() => auction != null && auction.Participants.Contains(Local));
        AuctionModel model = auction;
        int finished = finishedAuctions;
        yield return RunInspect();

        gate.AllowBid();
        yield return Show("tutorial.bid.intro", TutorialNavLayout.Top, true, () => model.BidOf(Local) > 0, TutorialPointerSide.Left, bidButton, amountReels);
        gate.Lock();
        yield return ShowAndWaitClick("tutorial.fold.leader", TutorialNavLayout.Top, true, foldButton);

        for (int round = 0; round < raiseRounds; round++)
        {
            yield return WaitUntilOutbid(model);
            if (!model.IsActive(Local))
                break;

            gate.AllowBid();
            yield return Show("tutorial.bid.raise", TutorialNavLayout.Top, true, () => !model.IsActive(Local) || model.IsLeading(Local), TutorialPointerSide.Left, bidButton, amountReels);
            gate.Lock();
        }

        yield return WaitUntilOutbid(model);
        if (model.IsActive(Local))
        {
            gate.AllowFold();
            yield return Show("tutorial.fold.outbid", TutorialNavLayout.Top, true, () => !model.IsActive(Local), foldButton);
            gate.Lock();
        }
        yield return LetCpusAct(finished);
    }

    /// <summary>CPU を動かし、自分が最高額でなくなる（またはオークションから外れる）まで待つ</summary>
    IEnumerator WaitUntilOutbid(AuctionModel model)
    {
        navigator.Hide();
        cpusMayAct = true;
        yield return new WaitUntil(() => !model.IsActive(Local) || !model.IsLeading(Local));
        cpusMayAct = false;
    }

    /// <summary>虫眼鏡でほかのプレイヤーの手元を見せ、見送り権のチケットと落札したカードを案内する</summary>
    IEnumerator RunInspect()
    {
        yield return Show("tutorial.inspect.open", TutorialNavLayout.Top, true, () => inspectCamera.IsInspecting, inspectButtons.Markers.ToArray());
        yield return ShowAndWaitClick("tutorial.inspect.view", TutorialNavLayout.Top, false);
    }

    /// <summary>ターン 3：アイテムの使い方を案内してから、ライバルの出品に入札して落札する</summary>
    IEnumerator RunBidding()
    {
        yield return new WaitUntil(() => auction != null && auction.Participants.Contains(Local));
        AuctionModel model = auction;
        int finished = finishedAuctions;
        yield return RunItems();

        gate.AllowBid();
        yield return Show("tutorial.bid.win", TutorialNavLayout.Top, true, () => model.BidOf(Local) > 0, TutorialPointerSide.Left, bidButton, amountReels);
        gate.Lock();
        yield return LetCpusAct(finished);
    }

    /// <summary>練習用のアイテムを渡し、虫眼鏡 → 電気プラグ → リモコンの順に 1 つずつ使わせる。案内中は使っている途中のアイテムしか送らない</summary>
    IEnumerator RunItems()
    {
        foreach (ItemDefinition item in practiceItems)
        {
            if (item != null)
                game.TryGiveItem(Local, item);
        }
        if (itemTable != null)
            yield return itemTable.PresentAcquired(Local);

        yield return ShowAndWaitClick("tutorial.item.get", TutorialNavLayout.Top, true, itemSlotsArea);
        yield return ShowAndWaitClick("tutorial.item.when", TutorialNavLayout.Top, true, itemSlotsArea);

        if (TryFindItem(ItemKind.Magnifier, out int magnifier))
        {
            gate.AllowUseItem(magnifier);
            yield return Show("tutorial.item.magnifier", TutorialNavLayout.Top, true, () => !HasItem(ItemKind.Magnifier), SlotRect(magnifier));
            gate.Lock();
            navigator.Hide();
            float deadline = Time.time + revealStartTimeout;
            yield return new WaitUntil(() => effectReveal.IsPlaying || Time.time > deadline);
            yield return new WaitWhile(() => effectReveal.IsPlaying);
        }

        if (TryFindItem(ItemKind.PowerPlug, out int plug))
        {
            yield return UseWithTarget(ItemKind.PowerPlug, plug, "tutorial.item.plug", "tutorial.item.plug_pick");
            yield return ShowAndWaitClick("tutorial.item.shield", TutorialNavLayout.Top, false);
        }

        if (TryFindItem(ItemKind.RemoteControl, out int remote))
        {
            yield return UseWithTarget(ItemKind.RemoteControl, remote, "tutorial.item.remote", "tutorial.item.remote_pick");
            yield return ShowAndWaitClick("tutorial.item.paralysis", TutorialNavLayout.Top, false);
        }
        navigator.Hide();
    }

    /// <summary>相手を選ぶアイテムを使わせる（マスを押す案内 → 相手を選ぶ案内。選ぶのをやめたらマスを押す案内へ戻る）</summary>
    IEnumerator UseWithTarget(ItemKind kind, int slot, string useKey, string pickKey)
    {
        gate.AllowUseItem(slot);
        while (HasItem(kind))
        {
            yield return Show(useKey, TutorialNavLayout.Top, true, () => targetPicker.IsPicking || !HasItem(kind), SlotRect(slot));
            if (!targetPicker.IsPicking)
                continue;

            yield return null;
            yield return Show(pickKey, TutorialNavLayout.Top, false, () => !targetPicker.IsPicking || !HasItem(kind), targetPicker.VisibleButtons);
        }
        gate.Lock();
    }

    bool HasItem(ItemKind kind) => TryFindItem(kind, out _);

    /// <summary>自分のマスのうち、kind のアイテムが入っている最初のマス</summary>
    bool TryFindItem(ItemKind kind, out int slot)
    {
        slot = -1;
        if (!game.TryGet(Local, out PlayerState me))
            return false;

        for (int i = 0; i < me.Items.Slots.Count; i++)
        {
            ItemDefinition item = me.Items.Slots[i];
            if (item != null && item.Kind == kind)
            {
                slot = i;
                return true;
            }
        }
        return false;
    }

    RectTransform SlotRect(int slot) => slot >= 0 && slot < itemSlots.Length ? itemSlots[slot] : itemSlotsArea;

    /// <summary>FINAL BATTLE で変わるルールを案内する。読んでいる間は CPU を止める</summary>
    IEnumerator RunFinalBattleRules()
    {
        hasExplainedFinalBattle = true;
        isExplaining = true;
        yield return ShowAndWaitClick("tutorial.final.intro", TutorialNavLayout.Top, true);
        yield return ShowAndWaitClick("tutorial.final.proceeds", TutorialNavLayout.Top, true);
        yield return ShowAndWaitClick("tutorial.final.drain", TutorialNavLayout.Top, true);
        isExplaining = false;
    }

    /// <summary>ターン 4：FINAL BATTLE のルールとスペシャルカード付きのカードを案内し、そこからは自由に操作させる</summary>
    IEnumerator RunSpecialListing()
    {
        yield return new WaitUntil(() => IsLocalListing);
        if (game.IsFinalDuel)
            yield return RunFinalBattleRules();

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

    /// <summary>自分から時計回りに steps 人目の CPU。並びが分からないときは番号がいちばん小さい CPU</summary>
    TutorialCpuAgent CpuClockwiseFromLocal(int steps)
    {
        IReadOnlyList<int> clockwise = turnFlow.ClockwiseSellers;
        int localIndex = clockwise.ToList().IndexOf(Local);
        if (localIndex >= 0 && cpus.TryGetValue(clockwise[(localIndex + steps) % clockwise.Count], out TutorialCpuAgent cpu))
            return cpu;
        return cpus.OrderBy(entry => entry.Key).Select(entry => entry.Value).FirstOrDefault();
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
