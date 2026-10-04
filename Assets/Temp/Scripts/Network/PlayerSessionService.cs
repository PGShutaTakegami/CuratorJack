using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// サーバー側で接続承認・プレイヤー番号・席の割り当てを管理する。
/// </summary>
public class PlayerSessionService : MonoBehaviour
{
    public const string SessionFullReason = "SessionFull";

    [Tooltip("同時に接続できる人数（Local は 2 人）")]
    [SerializeField] private int maxPlayers = 2;

    [SerializeField] private SeatRegistry seatRegistry;

    NetworkManager networkManager;
    PlayerSlotAllocator slotAllocator;
    SeatAssignmentPolicy seatPolicy;
    readonly Dictionary<int, int> seatByPlayer = new Dictionary<int, int>();

    public int MaxPlayers => maxPlayers;

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
        slotAllocator = new PlayerSlotAllocator(Mathf.Min(maxPlayers, seatRegistry.SeatCount));
        seatPolicy = new SeatAssignmentPolicy(seatRegistry.SeatCount, seatRegistry.FirstSeat, new System.Random());
        seatByPlayer.Clear();

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
        bool approved = slotAllocator.TryAssign(request.ClientNetworkId, out int playerNumber);
        response.Approved = approved;
        response.CreatePlayerObject = approved;
        response.Reason = approved ? string.Empty : SessionFullReason;

        if (approved)
            seatByPlayer[playerNumber] = seatPolicy.ChooseSeat(playerNumber, seatByPlayer);
    }

    void HandleClientConnected(ulong clientId)
    {
        if (!networkManager.IsServer || !slotAllocator.TryGetNumber(clientId, out int playerNumber))
            return;

        NetworkObject playerObject = networkManager.ConnectedClients[clientId].PlayerObject;
        if (playerObject == null || !playerObject.TryGetComponent(out NetworkSessionPlayer player))
            return;

        player.AssignByServer(playerNumber, seatByPlayer[playerNumber]);
        Debug.Log($"[Session] Player{playerNumber} が参加（clientId={clientId}, 席={seatByPlayer[playerNumber]}）");
    }

    void HandleClientDisconnected(ulong clientId)
    {
        if (!networkManager.IsServer || !slotAllocator.TryRelease(clientId, out int playerNumber))
            return;

        seatByPlayer.Remove(playerNumber);
        Debug.Log($"[Session] Player{playerNumber} が退出（clientId={clientId}）");
    }
}
