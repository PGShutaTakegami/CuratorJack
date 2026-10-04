using System;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// ネットワーク上のプレイヤー 1 人分の状態（プレイヤー番号・席）と入力通知。
/// 番号と席はサーバーだけが書き込み、全クライアントへ同期される。
/// </summary>
public class NetworkSessionPlayer : NetworkBehaviour
{
    public const int UnassignedNumber = 0;

    readonly NetworkVariable<int> playerNumber = new NetworkVariable<int>(UnassignedNumber);
    readonly NetworkVariable<int> seatIndex = new NetworkVariable<int>(SeatAssignmentPolicy.NoSeat);

    public int PlayerNumber => playerNumber.Value;

    public int SeatIndex => seatIndex.Value;

    public event Action<int> PlayerNumberChanged;

    public event Action<int> SeatIndexChanged;

    public override void OnNetworkSpawn()
    {
        playerNumber.OnValueChanged += HandlePlayerNumberChanged;
        seatIndex.OnValueChanged += HandleSeatIndexChanged;
        PlayerNumberChanged?.Invoke(playerNumber.Value);
        SeatIndexChanged?.Invoke(seatIndex.Value);
    }

    public override void OnNetworkDespawn()
    {
        playerNumber.OnValueChanged -= HandlePlayerNumberChanged;
        seatIndex.OnValueChanged -= HandleSeatIndexChanged;
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
}
