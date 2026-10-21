using UnityEngine;

/// <summary>
/// 出品・オークションの待ち時間を計り、時間切れになったら今の手続きを時間切れにする。
/// 待ち時間は今のプレイ形式のルーム設定から読み、無制限なら計らない。オークションは入札か降りるがあるたびに計り直す。
/// 時間切れを送るのは部屋主（SinglePlay は自分、マルチプレイはホスト）だけで、反映は全員の画面で同じ時点になる。
/// ゲームの時間で計るため、SinglePlay のポーズ中は止まる。
/// </summary>
public class PhaseTimer : MonoBehaviour
{
    [SerializeField] private TurnFlowController turnFlow;

    [SerializeField] private PlayerRegistry registry;

    [SerializeField] private LocalRoomOwner roomOwner;

    [Tooltip("IRoomControl を持つコンポーネント（SinglePlay / マルチプレイ）")]
    [SerializeField] private MonoBehaviour[] rooms;

    AuctionModel auction;
    bool hasTimedOut;

    /// <summary>今の手続きの待ち時間を計っているか</summary>
    public bool IsRunning { get; private set; }

    /// <summary>今の手続きが時間切れになるまでの残り秒数</summary>
    public float RemainingSeconds { get; private set; }

    void OnEnable()
    {
        turnFlow.ListingStarted += HandleListingStarted;
        turnFlow.ListingFinished += Stop;
        turnFlow.AuctionStarted += HandleAuctionStarted;
        turnFlow.AuctionFinished += HandleAuctionFinished;
    }

    void OnDisable()
    {
        turnFlow.ListingStarted -= HandleListingStarted;
        turnFlow.ListingFinished -= Stop;
        turnFlow.AuctionStarted -= HandleAuctionStarted;
        turnFlow.AuctionFinished -= HandleAuctionFinished;
        HandleAuctionFinished();
    }

    void Update()
    {
        if (!IsRunning || hasTimedOut)
            return;

        RemainingSeconds = Mathf.Max(0f, RemainingSeconds - Time.deltaTime);
        if (RemainingSeconds > 0f)
            return;

        hasTimedOut = true;
        if (roomOwner.IsOwner)
            turnFlow.Timeouts.TimeOut(registry.LocalPlayerNumber);
    }

    void HandleListingStarted(ListingSession session) => Restart();

    void HandleAuctionStarted(AuctionModel model)
    {
        auction = model;
        auction.BidPlaced += HandleBidPlaced;
        auction.Folded += HandleFolded;
        Restart();
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

    void HandleBidPlaced(int player, int amount) => Restart();

    void HandleFolded(int player) => Restart();

    void Restart()
    {
        int seconds = WaitSeconds();
        IsRunning = seconds != RoomWaitTime.Infinite;
        RemainingSeconds = seconds;
        hasTimedOut = false;
    }

    void Stop()
    {
        IsRunning = false;
        RemainingSeconds = 0f;
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
