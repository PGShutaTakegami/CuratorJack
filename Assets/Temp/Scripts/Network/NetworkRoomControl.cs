using System;
using System.Collections.Generic;
using System.Text;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// マルチプレイのルーム設定。人数・待ち時間・VC の ON/OFF は NetworkRoomState で全員へ同期し、変えられるのは部屋主（部屋主待ちでないホスト）だけ。
/// キックはゲーム開始前なら切断だけ、ゲーム中はリタイアを全員へ配ってから切断する（CPU は開始前なら外して空き枠に戻し、ゲーム中はリタイアだけ）。
/// 空き枠はホストがゲーム開始前に CPU で埋められる。
/// 部屋にいるプレイヤー（参加・退出・名前・CPU への切り替え）が変わったら知らせる。
/// </summary>
public class NetworkRoomControl : MonoBehaviour, IRoomControl
{
    [SerializeField] private NetworkRoomState roomState;

    [SerializeField] private PlayerSessionService sessionService;

    [SerializeField] private NetworkMatchBootstrap matchBootstrap;

    [SerializeField] private PlayerRegistry registry;

    [SerializeField] private TurnFlowController turnFlow;

    [SerializeField] private GameStartGate startGate;

    readonly List<RoomMember> members = new List<RoomMember>();
    readonly StringBuilder signature = new StringBuilder();
    string lastSignature = string.Empty;

    public bool IsActive => roomState.IsSpawned;

    public int Capacity => roomState.Capacity;

    public int MaxCapacity => sessionService.SeatCount;

    public IReadOnlyList<RoomMember> Members => members;

    public int WaitSeconds => roomState.WaitSeconds;

    public bool CanChangeWaitTime => IsHost;

    public bool CanFillWithCpu => IsHost;

    public bool IsVoiceChatOn => IsActive && roomState.IsVoiceChatOn;

    public bool CanChangeVoiceChat => IsHost;

    /// <summary>この部屋の VC のチャンネル名。決まるまでは空</summary>
    public string VoiceChannel => IsActive ? roomState.VoiceChannel : string.Empty;

    public event Action Changed;

    bool IsHost => roomState.IsLocalOwner;

    void OnEnable()
    {
        roomState.Changed += RaiseChanged;
    }

    void OnDisable()
    {
        roomState.Changed -= RaiseChanged;
    }

    void Update()
    {
        if (IsActive)
            RefreshMembers();
    }

    public bool SetCapacity(int capacity)
    {
        if (!IsHost || startGate.HasStarted || capacity < members.Count)
            return false;

        return roomState.SetCapacity(capacity);
    }

    public void SetWaitSeconds(int seconds)
    {
        if (IsHost && !startGate.HasStarted)
            roomState.SetWaitSeconds(seconds);
    }

    public void SetVoiceChat(bool on)
    {
        if (IsHost && !startGate.HasStarted)
            roomState.SetVoiceChat(on);
    }

    public bool FillWithCpu() => IsHost && !startGate.HasStarted && sessionService.TryAddCpu();

    public void Kick(int playerNumber)
    {
        if (!IsHost || playerNumber == registry.LocalPlayerNumber || !TryGetPlayer(playerNumber, out NetworkSessionPlayer player))
            return;

        if (startGate.HasStarted)
        {
            matchBootstrap.MarkKicked(playerNumber);
            turnFlow.Commands.Retire(playerNumber);
        }

        if (player.IsCpu)
        {
            if (!startGate.HasStarted)
                sessionService.RemoveCpu(player);
            return;
        }
        sessionService.Kick(playerNumber);
    }

    bool TryGetPlayer(int playerNumber, out NetworkSessionPlayer player)
    {
        player = null;
        return registry.TryGet(playerNumber, out Transform body) && body.TryGetComponent(out player) && player.IsSpawned;
    }

    void RefreshMembers()
    {
        members.Clear();
        signature.Clear();
        var numbers = new List<int>(registry.PlayerNumbers);
        numbers.Sort();
        foreach (int number in numbers)
        {
            if (!TryGetPlayer(number, out NetworkSessionPlayer player))
                continue;

            bool isCpu = player.IsCpu || matchBootstrap.IsCpu(number);
            members.Add(new RoomMember(number, isCpu, number == registry.LocalPlayerNumber));
            signature.Append(number).Append(isCpu ? 'c' : 'h').Append(player.DisplayName).Append('|');
        }

        string current = signature.ToString();
        if (current == lastSignature)
            return;

        lastSignature = current;
        RaiseChanged();
    }

    void RaiseChanged() => Changed?.Invoke();
}
