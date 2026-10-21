using System;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// マルチプレイの部屋の設定（人数・待ち時間）を全員へ同期する。書き込めるのはサーバー（ホスト）だけ。
/// 人数は準備完了に必要な人数と接続の上限に反映し、全員が部屋の情報へ控えておく（ホストの引き継ぎやリトライで同じ設定にするため）。
/// </summary>
public class NetworkRoomState : NetworkBehaviour
{
    [SerializeField] private PlayerSessionService sessionService;

    [SerializeField] private ReadyRoster readyRoster;

    readonly NetworkVariable<int> capacity = new NetworkVariable<int>();
    readonly NetworkVariable<int> waitSeconds = new NetworkVariable<int>(RoomWaitTime.Infinite);

    public int Capacity => capacity.Value;

    public int WaitSeconds => waitSeconds.Value;

    public event Action Changed;

    public override void OnNetworkSpawn()
    {
        capacity.OnValueChanged += HandleCapacityChanged;
        waitSeconds.OnValueChanged += HandleWaitSecondsChanged;
        if (IsServer)
        {
            bool hasRoom = RoomConnectionContext.HasRoom;
            sessionService.SetCapacity(hasRoom ? RoomConnectionContext.Room.capacity : sessionService.MaxPlayers);
            capacity.Value = sessionService.MaxPlayers;
            waitSeconds.Value = hasRoom ? RoomConnectionContext.Room.waitSeconds : RoomWaitTime.Infinite;
        }
        ApplyCapacity(capacity.Value);
        ApplyWaitSeconds(waitSeconds.Value);
    }

    public override void OnNetworkDespawn()
    {
        capacity.OnValueChanged -= HandleCapacityChanged;
        waitSeconds.OnValueChanged -= HandleWaitSecondsChanged;
    }

    /// <summary>サーバーのみ。今いる人数より少なくはできない</summary>
    public bool SetCapacity(int value)
    {
        if (!IsServer || !sessionService.SetCapacity(value))
            return false;

        capacity.Value = value;
        return true;
    }

    /// <summary>サーバーのみ</summary>
    public void SetWaitSeconds(int value)
    {
        if (IsServer)
            waitSeconds.Value = value;
    }

    void HandleCapacityChanged(int previous, int current) => ApplyCapacity(current);

    void HandleWaitSecondsChanged(int previous, int current) => ApplyWaitSeconds(current);

    void ApplyCapacity(int value)
    {
        if (value <= 0)
            return;

        readyRoster.SetTotal(value);
        if (RoomConnectionContext.HasRoom)
            RoomConnectionContext.Room.capacity = value;
        Changed?.Invoke();
    }

    void ApplyWaitSeconds(int value)
    {
        if (RoomConnectionContext.HasRoom)
            RoomConnectionContext.Room.waitSeconds = value;
        Changed?.Invoke();
    }
}
