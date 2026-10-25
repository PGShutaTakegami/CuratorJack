using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 出品・オークションの待ち時間を計り、時間切れになったら今の手続きを時間切れにする。
/// 待ち時間は今のプレイ形式のルーム設定から読み、無制限なら計らない。
/// 出品の始まり・オークションの始まり・入札・降りるごとにステップの番号を振り（全員の画面で同じ番号になる）、ステップごとに計り直す。
/// 数え始めるのはこの画面の演出（オークションでは他の人の入札のアップ）が終わってから。マルチプレイでは対象者全員の画面の演出が終わってから
/// ホストが決めた締め切りで数え、全員の画面で残り秒数をそろえる（NetworkPhaseClock）。SinglePlay は自分の画面だけで数え、ポーズ中は止まる。
/// 対象者は、出品では出品者、オークションでは降りていない参加者。
/// 時間切れを送るのは部屋主（SinglePlay は自分、マルチプレイはホスト）だけで、反映は全員の画面で同じ時点になる。
/// </summary>
public class PhaseTimer : MonoBehaviour
{
    [SerializeField] private TurnFlowController turnFlow;

    [SerializeField] private PlayerRegistry registry;

    [SerializeField] private LocalRoomOwner roomOwner;

    [Tooltip("IRoomControl を持つコンポーネント（SinglePlay / マルチプレイ）")]
    [SerializeField] private MonoBehaviour[] rooms;

    [Tooltip("他の人の入札をアップで映す演出。演出中は数え始めない")]
    [SerializeField] private BidderCloseUpShot bidderShot;

    [Tooltip("マルチプレイで全員の残り秒数をそろえる時計（マルチプレイのゲーム中でなければ SinglePlay の時計を使う）")]
    [SerializeField] private NetworkPhaseClock networkClock;

    readonly LocalPhaseClock localClock = new LocalPhaseClock();
    readonly List<int> targets = new List<int>();
    ListingSession listing;
    AuctionModel auction;
    int step;
    bool isTimed;
    bool hasReported;
    bool hasTimedOut;

    /// <summary>今のステップの待ち時間を数えているか（演出が終わるのを待っている間は false）</summary>
    public bool IsRunning { get; private set; }

    /// <summary>今の手続きが時間切れになるまでの残り秒数</summary>
    public float RemainingSeconds { get; private set; }

    /// <summary>今のステップの対象者か（出品では出品者、オークションでは降りていない参加者）</summary>
    public bool IsTarget(int player) => isTimed && targets.Contains(player);

    IPhaseClock Clock => networkClock != null && networkClock.IsActive ? networkClock : (IPhaseClock)localClock;

    /// <summary>この画面の演出が終わっている</summary>
    bool IsLocallyReady => auction == null || !bidderShot.IsPlaying;

    void OnEnable()
    {
        turnFlow.ListingStarted += HandleListingStarted;
        turnFlow.ListingFinished += HandleListingFinished;
        turnFlow.AuctionStarted += HandleAuctionStarted;
        turnFlow.AuctionFinished += HandleAuctionFinished;
    }

    void OnDisable()
    {
        turnFlow.ListingStarted -= HandleListingStarted;
        turnFlow.ListingFinished -= HandleListingFinished;
        turnFlow.AuctionStarted -= HandleAuctionStarted;
        turnFlow.AuctionFinished -= HandleAuctionFinished;
        HandleAuctionFinished();
    }

    void Update()
    {
        if (!isTimed)
            return;

        IPhaseClock clock = Clock;
        if (!hasReported && IsLocallyReady)
        {
            hasReported = true;
            clock.ReportReady(step);
        }

        if (!clock.TryGetRemaining(step, out float remaining))
            return;

        IsRunning = true;
        RemainingSeconds = remaining;
        if (remaining > 0f || hasTimedOut)
            return;

        hasTimedOut = true;
        if (roomOwner.IsOwner)
            turnFlow.Timeouts.TimeOut(registry.LocalPlayerNumber);
    }

    void HandleListingStarted(ListingSession session)
    {
        listing = session;
        BeginStep();
    }

    void HandleListingFinished()
    {
        listing = null;
        Stop();
    }

    void HandleAuctionStarted(AuctionModel model)
    {
        auction = model;
        auction.BidPlaced += HandleBidPlaced;
        auction.Folded += HandleFolded;
        BeginStep();
    }

    void HandleAuctionFinished()
    {
        if (auction != null)
        {
            auction.BidPlaced -= HandleBidPlaced;
            auction.Folded -= HandleFolded;
            auction = null;
        }
        Stop();
    }

    void HandleBidPlaced(int player, int amount) => BeginStep();

    void HandleFolded(int player) => BeginStep();

    void BeginStep()
    {
        step++;
        targets.Clear();
        if (auction != null)
            targets.AddRange(auction.ActivePlayers);
        else if (listing != null)
            targets.Add(listing.Seller);

        int seconds = WaitSeconds();
        isTimed = seconds != RoomWaitTime.Infinite;
        IsRunning = false;
        RemainingSeconds = seconds;
        hasReported = false;
        hasTimedOut = false;
        if (isTimed)
            Clock.BeginStep(step, targets, seconds);
    }

    void Stop()
    {
        isTimed = false;
        IsRunning = false;
        RemainingSeconds = 0f;
        targets.Clear();
        Clock.EndStep();
    }

    int WaitSeconds()
    {
        foreach (MonoBehaviour candidate in rooms)
        {
            if (candidate is IRoomControl room && room.IsActive)
                return room.WaitSeconds;
        }

        return RoomWaitTime.Infinite;
    }
}
