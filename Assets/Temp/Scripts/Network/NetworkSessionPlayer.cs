using System;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// ネットワーク上のプレイヤー 1 人分の状態（プレイヤー番号・席・準備完了）と入力通知。
/// 状態はサーバーだけが書き込み、全クライアントへ同期される。
/// </summary>
public class NetworkSessionPlayer : NetworkBehaviour
{
    public const int UnassignedNumber = 0;

    readonly NetworkVariable<int> playerNumber = new NetworkVariable<int>(UnassignedNumber);
    readonly NetworkVariable<int> seatIndex = new NetworkVariable<int>(SeatAssignmentPolicy.NoSeat);
    readonly NetworkVariable<bool> isReady = new NetworkVariable<bool>(false);

    public int PlayerNumber => playerNumber.Value;

    public int SeatIndex => seatIndex.Value;

    public bool IsReady => isReady.Value;

    public event Action<int> PlayerNumberChanged;

    public event Action<int> SeatIndexChanged;

    public event Action<bool> ReadyChanged;

    /// <summary>ホストがゲームを開始した（全員の画面で通知される）</summary>
    public event Action GameStarted;

    /// <summary>ホストが出品者を決めた（全員の画面で通知される）。出品者のプレイヤー番号を渡す</summary>
    public event Action<int> SellerDecided;

    public override void OnNetworkSpawn()
    {
        playerNumber.OnValueChanged += HandlePlayerNumberChanged;
        seatIndex.OnValueChanged += HandleSeatIndexChanged;
        isReady.OnValueChanged += HandleReadyChanged;
        PlayerNumberChanged?.Invoke(playerNumber.Value);
        SeatIndexChanged?.Invoke(seatIndex.Value);
        ReadyChanged?.Invoke(isReady.Value);
    }

    public override void OnNetworkDespawn()
    {
        playerNumber.OnValueChanged -= HandlePlayerNumberChanged;
        seatIndex.OnValueChanged -= HandleSeatIndexChanged;
        isReady.OnValueChanged -= HandleReadyChanged;
    }

    /// <summary>自分のプレイヤーの準備状態をサーバーへ送る。</summary>
    public void RequestReady(bool ready)
    {
        if (!IsSpawned || !IsOwner)
            return;

        RequestReadyRpc(ready);
    }

    [Rpc(SendTo.Server)]
    void RequestReadyRpc(bool ready, RpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId)
            return;

        isReady.Value = ready;
    }

    /// <summary>サーバー（ホスト）からゲーム開始を全員へ通知する。</summary>
    public void AnnounceGameStart()
    {
        if (!IsSpawned || !IsServer)
            return;

        NotifyGameStartedRpc();
    }

    [Rpc(SendTo.ClientsAndHost)]
    void NotifyGameStartedRpc()
    {
        GameStarted?.Invoke();
    }

    /// <summary>サーバー（ホスト）から決まった出品者を全員へ通知する。</summary>
    public void AnnounceSeller(int sellerNumber)
    {
        if (!IsSpawned || !IsServer)
            return;

        NotifySellerDecidedRpc(sellerNumber);
    }

    [Rpc(SendTo.ClientsAndHost)]
    void NotifySellerDecidedRpc(int sellerNumber)
    {
        SellerDecided?.Invoke(sellerNumber);
    }

    public void AssignByServer(int number, int seat)
    {
        if (!IsServer)
            return;

        playerNumber.Value = number;
        seatIndex.Value = seat;
    }

    /// <summary>自分のプレイヤーの決定入力をサーバーへ送る。</summary>
    public void RequestPush()
    {
        if (!IsSpawned || !IsOwner || PlayerNumber == UnassignedNumber)
            return;

        RequestPushRpc();
    }

    [Rpc(SendTo.Server)]
    void RequestPushRpc(RpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId)
            return;

        NotifyPushRpc(playerNumber.Value);
    }

    [Rpc(SendTo.ClientsAndHost)]
    void NotifyPushRpc(int number)
    {
        Debug.Log($"Player{number}Push");
    }

    void HandlePlayerNumberChanged(int previous, int current)
    {
        PlayerNumberChanged?.Invoke(current);
    }

    void HandleSeatIndexChanged(int previous, int current)
    {
        SeatIndexChanged?.Invoke(current);
    }

    void HandleReadyChanged(bool previous, bool current)
    {
        ReadyChanged?.Invoke(current);
    }
}
