using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// マルチプレイの部屋の設定（人数・待ち時間・VC の ON/OFF）と、この部屋の VC のチャンネル名を全員へ同期する。書き込めるのはサーバー（ホスト）だけ。
/// 人数は準備完了に必要な人数と接続の上限に反映し、全員が部屋の情報へ控えておく（ホストの引き継ぎやリトライで同じ設定にするため）。
/// VC のチャンネル名は、ホストが部屋を開くたびに作り直す（リトライ前の部屋の VC と混ざらないため）。
/// リトライで作り直した部屋では、元の部屋主の戻りを待つ間ホストは部屋主にならない（部屋主待ち）。部屋主はホストを務めていて部屋主待ちでない画面。
/// </summary>
public class NetworkRoomState : NetworkBehaviour
{
    [SerializeField] private PlayerSessionService sessionService;

    [SerializeField] private ReadyRoster readyRoster;

    readonly NetworkVariable<int> capacity = new NetworkVariable<int>();
    readonly NetworkVariable<int> waitSeconds = new NetworkVariable<int>(RoomWaitTime.Infinite);
    readonly NetworkVariable<bool> voiceChatOn = new NetworkVariable<bool>(true);
    readonly NetworkVariable<FixedString64Bytes> voiceChannel = new NetworkVariable<FixedString64Bytes>();
    readonly NetworkVariable<bool> ownerPending = new NetworkVariable<bool>();

    public int Capacity => capacity.Value;

    /// <summary>元の部屋主の戻りを待っていて、まだ誰も部屋主でないか</summary>
    public bool IsOwnerPending => IsSpawned && ownerPending.Value;

    /// <summary>この画面が部屋主か</summary>
    public bool IsLocalOwner => IsSpawned && IsServer && !ownerPending.Value;

    public int WaitSeconds => waitSeconds.Value;

    public bool IsVoiceChatOn => voiceChatOn.Value;

    /// <summary>この部屋の VC のチャンネル名。ホストが決めるまでは空</summary>
    public string VoiceChannel => voiceChannel.Value.ToString();

    public event Action Changed;

    /// <summary>サーバーのみ。元の部屋主だった画面が部屋主に戻りたいと申し出た（申し出た画面のクライアント ID, 直前の部屋の ID, オンラインのプレイヤー ID）</summary>
    public event Action<ulong, string, string> OwnershipRequested;

    /// <summary>全員。今のホストが抜けたあと、次のホストになる画面のクライアント ID が決まった</summary>
    public event Action<ulong> NextHostAnnounced;

    public override void OnNetworkSpawn()
    {
        capacity.OnValueChanged += HandleCapacityChanged;
        waitSeconds.OnValueChanged += HandleWaitSecondsChanged;
        voiceChatOn.OnValueChanged += HandleVoiceChatChanged;
        voiceChannel.OnValueChanged += HandleVoiceChannelChanged;
        ownerPending.OnValueChanged += HandleOwnerPendingChanged;
        if (IsServer)
        {
            bool hasRoom = RoomConnectionContext.HasRoom;
            sessionService.SetCapacity(hasRoom ? RoomConnectionContext.Room.capacity : sessionService.MaxPlayers);
            capacity.Value = sessionService.MaxPlayers;
            waitSeconds.Value = hasRoom ? RoomConnectionContext.Room.waitSeconds : RoomWaitTime.Infinite;
            voiceChatOn.Value = !hasRoom || !RoomConnectionContext.Room.voiceChatOff;
            voiceChannel.Value = new FixedString64Bytes(Guid.NewGuid().ToString("N"));
        }
        ApplyCapacity(capacity.Value);
        ApplyWaitSeconds(waitSeconds.Value);
        ApplyVoiceChat(voiceChatOn.Value);
    }

    public override void OnNetworkDespawn()
    {
        capacity.OnValueChanged -= HandleCapacityChanged;
        waitSeconds.OnValueChanged -= HandleWaitSecondsChanged;
        voiceChatOn.OnValueChanged -= HandleVoiceChatChanged;
        voiceChannel.OnValueChanged -= HandleVoiceChannelChanged;
        ownerPending.OnValueChanged -= HandleOwnerPendingChanged;
    }

    /// <summary>サーバーのみ</summary>
    public void SetOwnerPending(bool pending)
    {
        if (IsServer)
            ownerPending.Value = pending;
    }

    /// <summary>クライアントのみ。元の部屋主として部屋主に戻りたいとサーバーへ申し出る</summary>
    public void RequestOwnership(string previousRoomId, string playerId)
    {
        if (IsSpawned && !IsServer)
            RequestOwnershipRpc(previousRoomId ?? string.Empty, playerId ?? string.Empty);
    }

    /// <summary>サーバーのみ。ホストが抜ける前に、次のホストを全員へ知らせる</summary>
    public void AnnounceNextHost(ulong clientId)
    {
        if (IsServer)
            NotifyNextHostRpc(clientId);
    }

    [Rpc(SendTo.Server)]
    void RequestOwnershipRpc(string previousRoomId, string playerId, RpcParams rpcParams = default)
    {
        OwnershipRequested?.Invoke(rpcParams.Receive.SenderClientId, previousRoomId, playerId);
    }

    [Rpc(SendTo.ClientsAndHost)]
    void NotifyNextHostRpc(ulong clientId)
    {
        NextHostAnnounced?.Invoke(clientId);
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

    /// <summary>サーバーのみ</summary>
    public void SetVoiceChat(bool on)
    {
        if (IsServer)
            voiceChatOn.Value = on;
    }

    void HandleCapacityChanged(int previous, int current) => ApplyCapacity(current);

    void HandleWaitSecondsChanged(int previous, int current) => ApplyWaitSeconds(current);

    void HandleVoiceChatChanged(bool previous, bool current) => ApplyVoiceChat(current);

    void HandleVoiceChannelChanged(FixedString64Bytes previous, FixedString64Bytes current) => Changed?.Invoke();

    void HandleOwnerPendingChanged(bool previous, bool current) => Changed?.Invoke();

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

    void ApplyVoiceChat(bool on)
    {
        if (RoomConnectionContext.HasRoom)
            RoomConnectionContext.Room.voiceChatOff = !on;
        Changed?.Invoke();
    }
}
