using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// ネットワーク上のプレイヤー 1 人分の状態（プレイヤー番号・席・準備完了・名前）と、ゲームの進行に関わる通知。
/// 状態はサーバーだけが書き込み、全クライアントへ同期される。
/// ゲーム中の操作は、自分のプレイヤーからサーバーへ送り、サーバーが受け取った順に全員へ配る（全員の画面で同じ順に反映するため）。
/// ホストがルーム設定で空き枠を埋めた CPU も、サーバーが持つこのプレイヤーとして生成する（自分のプレイヤーかは IsLocalPlayer で判断する）。
/// </summary>
public class NetworkSessionPlayer : NetworkBehaviour, IPlayerActionChannel
{
    public const int UnassignedNumber = 0;

    readonly NetworkVariable<int> playerNumber = new NetworkVariable<int>(UnassignedNumber);
    readonly NetworkVariable<int> seatIndex = new NetworkVariable<int>(SeatAssignmentPolicy.NoSeat);
    readonly NetworkVariable<bool> isReady = new NetworkVariable<bool>(false);
    readonly NetworkVariable<FixedString64Bytes> displayName = new NetworkVariable<FixedString64Bytes>();
    readonly NetworkVariable<bool> isCpu = new NetworkVariable<bool>(false);

    public int PlayerNumber => playerNumber.Value;

    /// <summary>ホストがルーム設定で空き枠を埋めた CPU か</summary>
    public bool IsCpu => isCpu.Value;

    /// <summary>ロビーで入力された名前。未入力なら空</summary>
    public string DisplayName => displayName.Value.ToString();

    public int SeatIndex => seatIndex.Value;

    public bool IsReady => isReady.Value;

    public event Action<int> PlayerNumberChanged;

    public event Action<int> SeatIndexChanged;

    public event Action<bool> ReadyChanged;

    public event Action<string> DisplayNameChanged;

    /// <summary>ホストがゲームを開始した（全員の画面で通知される）。全員で山札と効果の乱数をそろえるシードを渡す</summary>
    public event Action<int> GameStarted;

    /// <summary>ホストが出品者を決めた（全員の画面で通知される）。出品者のプレイヤー番号を渡す</summary>
    public event Action<int> SellerDecided;

    /// <summary>このプレイヤーの操作が、サーバーから配られた（全員の画面で同じ順に通知される）</summary>
    public event Action<PlayerAction> ActionReceived;

    /// <summary>このプレイヤーが切断し、ホストの CPU が代わりに操作し始めた（全員の画面で通知される）。プレイヤー番号を渡す</summary>
    public event Action<int> CpuTookOver;

    /// <summary>サーバーのみ：このプレイヤーの画面で、待ち時間のステップの演出が終わった。プレイヤー番号とステップを渡す</summary>
    public event Action<int, int> PhaseReadyReported;

    /// <summary>ホストが待ち時間のステップの締め切り（ネットワークの時刻）を決めた（全員の画面で通知される）。ステップと締め切りを渡す</summary>
    public event Action<int, double> PhaseCountdownAnnounced;

    public override void OnNetworkSpawn()
    {
        playerNumber.OnValueChanged += HandlePlayerNumberChanged;
        seatIndex.OnValueChanged += HandleSeatIndexChanged;
        isReady.OnValueChanged += HandleReadyChanged;
        displayName.OnValueChanged += HandleDisplayNameChanged;
        PlayerNumberChanged?.Invoke(playerNumber.Value);
        SeatIndexChanged?.Invoke(seatIndex.Value);
        ReadyChanged?.Invoke(isReady.Value);
        DisplayNameChanged?.Invoke(DisplayName);
    }

    public override void OnNetworkDespawn()
    {
        playerNumber.OnValueChanged -= HandlePlayerNumberChanged;
        seatIndex.OnValueChanged -= HandleSeatIndexChanged;
        isReady.OnValueChanged -= HandleReadyChanged;
        displayName.OnValueChanged -= HandleDisplayNameChanged;
    }

    /// <summary>自分のプレイヤーの名前をサーバーへ送る。</summary>
    public void RequestDisplayName(string name)
    {
        if (!IsSpawned || !IsOwner)
            return;

        RequestDisplayNameRpc(PlayerNameContext.Sanitize(name));
    }

    [Rpc(SendTo.Server)]
    void RequestDisplayNameRpc(string name, RpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId)
            return;

        var value = new FixedString64Bytes();
        value.CopyFromTruncated(PlayerNameContext.Sanitize(name));
        displayName.Value = value;
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

    /// <summary>サーバー（ホスト）からゲーム開始と乱数のシードを全員へ通知する。</summary>
    public void AnnounceGameStart(int seed)
    {
        if (!IsSpawned || !IsServer)
            return;

        NotifyGameStartedRpc(seed);
    }

    [Rpc(SendTo.ClientsAndHost)]
    void NotifyGameStartedRpc(int seed)
    {
        GameStarted?.Invoke(seed);
    }

    /// <summary>自分のプレイヤーの操作をサーバーへ送る。</summary>
    public void Send(PlayerAction action)
    {
        if (!IsSpawned || !IsOwner || PlayerNumber == UnassignedNumber || action.Player != PlayerNumber)
            return;

        RequestActionRpc(action.Phase, (int)action.Kind, action.Value);
    }

    [Rpc(SendTo.Server)]
    void RequestActionRpc(int phase, int kind, int value, RpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId)
            return;
        if ((PlayerActionKind)kind == PlayerActionKind.Timeout && rpcParams.Receive.SenderClientId != NetworkManager.ServerClientId)
            return;

        NotifyActionRpc(phase, kind, value);
    }

    /// <summary>サーバー（ホスト）で動く CPU の操作を、このプレイヤーの操作として全員へ配る（切断した人の代わり）。</summary>
    public void SendByServer(PlayerAction action)
    {
        if (!IsSpawned || !IsServer || action.Player != PlayerNumber)
            return;

        NotifyActionRpc(action.Phase, (int)action.Kind, action.Value);
    }

    /// <summary>サーバー（ホスト）から、このプレイヤーを CPU が引き継いだことを全員へ通知する。</summary>
    public void AnnounceCpuTakeover()
    {
        if (!IsSpawned || !IsServer)
            return;

        NotifyCpuTakeoverRpc();
    }

    [Rpc(SendTo.ClientsAndHost)]
    void NotifyCpuTakeoverRpc()
    {
        CpuTookOver?.Invoke(playerNumber.Value);
    }

    [Rpc(SendTo.ClientsAndHost)]
    void NotifyActionRpc(int phase, int kind, int value)
    {
        ActionReceived?.Invoke(new PlayerAction(phase, playerNumber.Value, (PlayerActionKind)kind, value));
    }

    /// <summary>自分のプレイヤーの画面で、待ち時間のステップの演出が終わったことをサーバーへ送る。</summary>
    public void ReportPhaseReady(int step)
    {
        if (!IsSpawned || !IsOwner)
            return;

        ReportPhaseReadyRpc(step);
    }

    [Rpc(SendTo.Server)]
    void ReportPhaseReadyRpc(int step, RpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId)
            return;

        PhaseReadyReported?.Invoke(playerNumber.Value, step);
    }

    /// <summary>サーバー（ホスト）から、待ち時間のステップの締め切りを全員へ通知する。</summary>
    public void AnnouncePhaseCountdown(int step, double deadline)
    {
        if (!IsSpawned || !IsServer)
            return;

        NotifyPhaseCountdownRpc(step, deadline);
    }

    [Rpc(SendTo.ClientsAndHost)]
    void NotifyPhaseCountdownRpc(int step, double deadline)
    {
        PhaseCountdownAnnounced?.Invoke(step, deadline);
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

    /// <summary>サーバーのみ：空き枠を埋める CPU として番号と席を割り当て、最初から準備完了にする</summary>
    public void AssignCpuByServer(int number, int seat)
    {
        if (!IsServer)
            return;

        isCpu.Value = true;
        isReady.Value = true;
        AssignByServer(number, seat);
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

    void HandleDisplayNameChanged(FixedString64Bytes previous, FixedString64Bytes current)
    {
        DisplayNameChanged?.Invoke(current.ToString());
    }
}
