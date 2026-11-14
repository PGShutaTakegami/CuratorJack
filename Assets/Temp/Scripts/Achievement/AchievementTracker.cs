using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// ゲームの出来事を見て、自分（このパソコンのプレイヤー）の実績を解除する。ゲームの進行には触らない。
/// チュートリアルでは、クリアの実績だけを解除する（ほかの実績はチュートリアルの決まった流れでは取らせない）。
/// 落札は自分の落札した商品が増えたこと、出品が売れたことは直前のオークションの出品者と落札者で判定する。
/// </summary>
public class AchievementTracker : MonoBehaviour
{
    [SerializeField] private GameManager game;

    [SerializeField] private TurnFlowController turnFlow;

    [SerializeField] private PlayerRegistry registry;

    [Tooltip("シングルプレイかどうかの判定に使う（動いていなければマルチプレイ）")]
    [SerializeField] private SinglePlaySessionBootstrap singlePlay;

    [Tooltip("リタイアの判定に使う")]
    [SerializeField] private PauseMenu pauseMenu;

    [Tooltip("チュートリアルのシーンでだけ設定する")]
    [SerializeField] private TutorialDirector tutorial;

    [Tooltip("1 回のゲームで落札するとコレクターになる枚数")]
    [SerializeField, Min(1)] private int collectorCount = 5;

    readonly Dictionary<int, int> collectionCounts = new Dictionary<int, int>();
    AuctionModel lastAuction;
    bool usedPassRight;
    int localDiedFrame = -1;

    int Local => registry.LocalPlayerNumber;

    bool IsMultiplayer => singlePlay == null || !singlePlay.IsActive;

    void OnEnable()
    {
        if (tutorial != null)
        {
            tutorial.Cleared += HandleTutorialCleared;
            return;
        }

        game.StatusChanged += HandleStatusChanged;
        game.PlayerDied += HandlePlayerDied;
        game.PassRightConsumed += HandlePassRightConsumed;
        turnFlow.AuctionStarted += HandleAuctionStarted;
        turnFlow.GameFinished += HandleGameFinished;
        if (pauseMenu != null)
            pauseMenu.RetireClicked += HandleRetireClicked;
    }

    void OnDisable()
    {
        if (tutorial != null)
        {
            tutorial.Cleared -= HandleTutorialCleared;
            return;
        }

        game.StatusChanged -= HandleStatusChanged;
        game.PlayerDied -= HandlePlayerDied;
        game.PassRightConsumed -= HandlePassRightConsumed;
        turnFlow.AuctionStarted -= HandleAuctionStarted;
        turnFlow.GameFinished -= HandleGameFinished;
        if (pauseMenu != null)
            pauseMenu.RetireClicked -= HandleRetireClicked;
    }

    void HandleTutorialCleared() => AchievementService.Unlock(AchievementIds.TutorialClear);

    void HandleAuctionStarted(AuctionModel auction)
    {
        lastAuction = auction;
        Listing listing = auction.Listing;
        if (listing.Seller == Local && listing.Goods.Special != null)
            AchievementService.Unlock(AchievementIds.SpecialListing);
    }

    /// <summary>落札した商品が増えたプレイヤーがいれば、直前のオークションで落札が成立した</summary>
    void HandleStatusChanged(PlayerState player)
    {
        int previous = collectionCounts.TryGetValue(player.PlayerNumber, out int count) ? count : 0;
        int current = player.Collection.Count;
        collectionCounts[player.PlayerNumber] = current;
        if (current <= previous)
            return;

        if (player.PlayerNumber == Local)
        {
            AchievementService.Unlock(AchievementIds.FirstPurchase);
            if (current >= collectorCount)
                AchievementService.Unlock(AchievementIds.Collector);
            if (lastAuction != null && lastAuction.Winner == Local && lastAuction.DrainAmount > 0 && CoinsOf(lastAuction.Listing.Seller) > 0)
                AchievementService.Unlock(AchievementIds.Drain);
        }

        if (lastAuction != null && lastAuction.Listing.Seller == Local && lastAuction.Winner == player.PlayerNumber)
            AchievementService.Unlock(AchievementIds.FirstSale);
    }

    void HandlePassRightConsumed(PlayerState player, int count)
    {
        if (player.PlayerNumber == Local)
            usedPassRight = true;
    }

    void HandlePlayerDied(PlayerState player)
    {
        if (player.PlayerNumber == Local)
            localDiedFrame = Time.frameCount;
        if (game.IsFinalDuel && game.IsAlive(Local))
            AchievementService.Unlock(AchievementIds.FinalBattle);
    }

    /// <summary>リタイアを押した時点で自分が生きていた（同じフレームでリタイアにより脱落した場合も含む）</summary>
    void HandleRetireClicked()
    {
        if (game.IsAlive(Local) || localDiedFrame == Time.frameCount)
            AchievementService.Unlock(AchievementIds.Retire);
    }

    void HandleGameFinished()
    {
        PlayerState winner = game.AlivePlayers.FirstOrDefault();
        if (winner == null || winner.PlayerNumber != Local)
            return;

        AchievementService.Unlock(AchievementIds.FirstWin);
        if (IsMultiplayer)
            AchievementService.Unlock(AchievementIds.OnlineWin);
        if (winner.PassRights == 0)
            AchievementService.Unlock(AchievementIds.LastStand);
        if (!usedPassRight)
            AchievementService.Unlock(AchievementIds.Flawless);
    }

    int CoinsOf(int player) => game.TryGet(player, out PlayerState state) ? state.Coins : 0;
}
