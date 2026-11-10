using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// ゲームの出来事を見て、自分（このパソコンのプレイヤー）の戦績を数えて保存する。ゲームの進行には触らない。
/// 本編のシーンにだけ置く（チュートリアルは数えない）。
/// 落札は自分の落札した商品が増えたこと、出品が売れたことは直前のオークションの出品者と落札者で判定する。
/// </summary>
public class PlayerRecordTracker : MonoBehaviour
{
    [SerializeField] private GameManager game;

    [SerializeField] private TurnFlowController turnFlow;

    [SerializeField] private PlayerRegistry registry;

    readonly Dictionary<int, int> collectionCounts = new Dictionary<int, int>();
    AuctionModel lastAuction;

    int Local => registry.LocalPlayerNumber;

    void OnEnable()
    {
        game.StatusChanged += HandleStatusChanged;
        turnFlow.RoundStarted += HandleRoundStarted;
        turnFlow.AuctionStarted += HandleAuctionStarted;
        turnFlow.ItemEffects.Used += HandleItemUsed;
        turnFlow.GameFinished += HandleGameFinished;
    }

    void OnDisable()
    {
        game.StatusChanged -= HandleStatusChanged;
        turnFlow.RoundStarted -= HandleRoundStarted;
        turnFlow.AuctionStarted -= HandleAuctionStarted;
        turnFlow.ItemEffects.Used -= HandleItemUsed;
        turnFlow.GameFinished -= HandleGameFinished;
    }

    void HandleRoundStarted(int round)
    {
        if (round == 1 && game.TryGet(Local, out _))
            PlayerRecordService.Change(record => record.AddGamePlayed());
    }

    void HandleAuctionStarted(AuctionModel auction) => lastAuction = auction;

    /// <summary>落札した商品が増えたプレイヤーがいれば、直前のオークションで落札が成立した</summary>
    void HandleStatusChanged(PlayerState player)
    {
        int previous = collectionCounts.TryGetValue(player.PlayerNumber, out int count) ? count : 0;
        int current = player.Collection.Count;
        collectionCounts[player.PlayerNumber] = current;
        if (current <= previous)
            return;

        if (player.PlayerNumber == Local)
            PlayerRecordService.Change(record => record.AddPurchase());
        if (lastAuction != null && lastAuction.Listing.Seller == Local && lastAuction.Winner == player.PlayerNumber)
            PlayerRecordService.Change(record => record.AddSale());
    }

    void HandleItemUsed(ItemUse use)
    {
        if (use.Player == Local)
            PlayerRecordService.Change(record => record.AddItemUsed());
    }

    void HandleGameFinished()
    {
        PlayerState winner = game.AlivePlayers.FirstOrDefault();
        if (winner != null && winner.PlayerNumber == Local)
            PlayerRecordService.Change(record => record.AddWin());
    }
}
