using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// サーバー側で接続承認・プレイヤー番号・席の割り当てを管理する。
/// 部屋の人数はルーム設定であとから変えられ、部屋主はプレイヤーをキック（切断）したり、空き枠を CPU で埋めたりできる。
/// CPU はプレイヤーと同じプレハブをサーバーが持つ形で生成し、番号と席を割り当てる。
/// 接続するときは自分のゲームのバージョンを送り、サーバーはバージョンが違う接続を断る。
/// </summary>
public class PlayerSessionService : MonoBehaviour
{
    public const string SessionFullReason = "SessionFull";

    public const string GameStartedReason = "GameStarted";

    public const string KickedReason = "Kicked";

    public const string VersionMismatchReason = "VersionMismatch";

    [Tooltip("同時に接続できる人数（Local は 2 人）")]
    [SerializeField] private int maxPlayers = 2;

    [SerializeField] private SeatRegistry seatRegistry;

    NetworkManager networkManager;
    PlayerSlotAllocator slotAllocator;
    SeatAssignmentPolicy seatPolicy;
    readonly Dictionary<int, int> seatByPlayer = new Dictionary<int, int>();

    bool isEntryClosed;
    IConnectionGuard guard;

    public int MaxPlayers => maxPlayers;

    /// <summary>接続の許可に通信方式ごとの確認を足す（null なら足さない）</summary>
    public void SetGuard(IConnectionGuard connectionGuard)
    {
        guard = connectionGuard;
    }

    /// <summary>接続するときにホストへ送るプレイヤー ID を決める（オンラインのみ。Bind のあとに呼ぶ）</summary>
    public void SetLocalPlayerId(string playerId)
    {
        if (networkManager != null)
            networkManager.NetworkConfig.ConnectionData = new ConnectionPayload(GameVersion.Current, playerId).ToBytes();
    }

    /// <summary>自分の接続が、ホストとゲームのバージョンが違うため断られたか</summary>
    public static bool WasRejectedByVersion(NetworkManager manager)
    {
        string reason = manager.DisconnectReason;
        return !string.IsNullOrEmpty(reason) && reason.Contains(VersionMismatchReason);
    }

    public int SeatCount => seatRegistry.SeatCount;

    /// <summary>接続中で番号を割り当てたプレイヤーの数（サーバーのみ）</summary>
    public int AssignedCount => slotAllocator != null ? slotAllocator.Count : 0;

    /// <summary>部屋の人数を変える（サーバーのみ）。今いる人数より少なくはできない</summary>
    public bool SetCapacity(int capacity)
    {
        if (slotAllocator == null || capacity < slotAllocator.Count || capacity > seatRegistry.SeatCount)
            return false;

        maxPlayers = capacity;
        slotAllocator.SetMaxPlayers(capacity);
        return true;
    }

    /// <summary>自分が部屋主にキックされて切断されたか</summary>
    public static bool WasKicked(NetworkManager manager)
    {
        string reason = manager.DisconnectReason;
        return !string.IsNullOrEmpty(reason) && reason.Contains(KickedReason);
    }

    /// <summary>プレイヤーを部屋から外す（サーバーのみ）。外された側には KickedReason が届く</summary>
    public bool Kick(int playerNumber)
    {
        if (networkManager == null || !networkManager.IsServer || slotAllocator == null
            || !slotAllocator.TryGetClient(playerNumber, out ulong clientId) || clientId == NetworkManager.ServerClientId)
            return false;

        guard?.HandleKicked(clientId);
        networkManager.DisconnectClient(clientId, KickedReason);
        return true;
    }

    /// <summary>空き枠を 1 つ CPU で埋める（サーバーのみ）</summary>
    public bool TryAddCpu()
    {
        if (networkManager == null || !networkManager.IsServer || isEntryClosed || !slotAllocator.TryAssignCpu(out int playerNumber))
            return false;

        int seat = seatPolicy.ChooseSeat(playerNumber, seatByPlayer);
        if (seat == SeatAssignmentPolicy.NoSeat)
        {
            slotAllocator.ReleaseCpu(playerNumber);
            return false;
        }

        seatByPlayer[playerNumber] = seat;
        GameObject cpu = Instantiate(networkManager.NetworkConfig.PlayerPrefab);
        cpu.name = $"CPU_Player{playerNumber}";
        cpu.GetComponent<NetworkObject>().Spawn(true);
        cpu.GetComponent<NetworkSessionPlayer>().AssignCpuByServer(playerNumber, seat);
        Debug.Log(GameText.Format("log.session.cpu_filled", playerNumber, seat));
        return true;
    }

    /// <summary>CPU を部屋から外して空き枠に戻す（サーバーのみ）</summary>
    public bool RemoveCpu(NetworkSessionPlayer cpu)
    {
        if (networkManager == null || !networkManager.IsServer || cpu == null || !slotAllocator.ReleaseCpu(cpu.PlayerNumber))
            return false;

        seatByPlayer.Remove(cpu.PlayerNumber);
        cpu.NetworkObject.Despawn(true);
        return true;
    }

    /// <summary>ゲームが始まったら、それ以降の参加を断る（切断で空いた番号に途中から入れないようにする）。</summary>
    public void CloseEntry()
    {
        isEntryClosed = true;
    }

    /// <summary>接続開始前に呼ぶ。</summary>
    public void Bind(NetworkManager manager)
    {
        Bind(manager, maxPlayers);
    }

    /// <summary>接続開始前に呼ぶ。同時接続数の上限を指定する（席の数を超えない）。</summary>
    public void Bind(NetworkManager manager, int playerLimit)
    {
        maxPlayers = playerLimit;
        networkManager = manager;
        slotAllocator = new PlayerSlotAllocator(maxPlayers, seatRegistry.SeatCount);
        seatPolicy = new SeatAssignmentPolicy(seatRegistry.SeatCount, seatRegistry.FirstSeat, new System.Random());
        seatByPlayer.Clear();

        networkManager.NetworkConfig.ConnectionData = new ConnectionPayload(GameVersion.Current, string.Empty).ToBytes();
        networkManager.NetworkConfig.ConnectionApproval = true;
        networkManager.ConnectionApprovalCallback = ApproveConnection;
        networkManager.OnClientConnectedCallback += HandleClientConnected;
        networkManager.OnClientDisconnectCallback += HandleClientDisconnected;
    }

    void OnDestroy()
    {
        if (networkManager == null)
            return;

        networkManager.ConnectionApprovalCallback = null;
        networkManager.OnClientConnectedCallback -= HandleClientConnected;
        networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
    }

    void ApproveConnection(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        if (isEntryClosed && request.ClientNetworkId != NetworkManager.ServerClientId)
        {
            response.Approved = false;
            response.Reason = GameStartedReason;
            return;
        }

        if (request.ClientNetworkId != NetworkManager.ServerClientId)
        {
            ConnectionPayload payload = ConnectionPayload.Parse(request.Payload);
            if (!GameVersion.IsCompatible(payload.Version))
            {
                response.Approved = false;
                response.Reason = VersionMismatchReason;
                return;
            }

            if (guard != null && !guard.Accepts(payload))
            {
                response.Approved = false;
                response.Reason = KickedReason;
                return;
            }
        }

        bool approved = slotAllocator.TryAssign(request.ClientNetworkId, out int playerNumber);
        response.Approved = approved;
        response.CreatePlayerObject = approved;
        response.Reason = approved ? string.Empty : SessionFullReason;

        if (approved)
            seatByPlayer[playerNumber] = seatPolicy.ChooseSeat(playerNumber, seatByPlayer);
    }

    void HandleClientConnected(ulong clientId)
    {
        if (networkManager.IsServer)
            guard?.HandleConnected(clientId);

        if (!networkManager.IsServer || !slotAllocator.TryGetNumber(clientId, out int playerNumber))
            return;

        NetworkObject playerObject = networkManager.ConnectedClients[clientId].PlayerObject;
        if (playerObject == null || !playerObject.TryGetComponent(out NetworkSessionPlayer player))
            return;

        player.AssignByServer(playerNumber, seatByPlayer[playerNumber]);
        Debug.Log(GameText.Format("log.session.player_joined", playerNumber, clientId, seatByPlayer[playerNumber]));
    }

    void HandleClientDisconnected(ulong clientId)
    {
        if (networkManager.IsServer)
            guard?.HandleDisconnected(clientId);

        if (!networkManager.IsServer || !slotAllocator.TryRelease(clientId, out int playerNumber))
            return;

        seatByPlayer.Remove(playerNumber);
        Debug.Log(GameText.Format("log.session.player_left", playerNumber, clientId));
    }
}
