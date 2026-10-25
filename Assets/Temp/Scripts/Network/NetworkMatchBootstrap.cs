using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// ネットワークでゲームが開始されたら、全員の画面でターンの進行を始める。
/// 自分は画面の操作役（LocalPlayerAgent）、他の人はネットワーク越しの操作役（RemotePlayerAgent）にし、
/// 操作は自分のプレイヤーからサーバー経由で全員へ配り、全プレイヤーから配られた操作を進行へ渡す。
/// 山札と効果の乱数は、ホストから届いたシードで全員そろえる。出品・オークションの待ち時間は NetworkPhaseClock で全員そろえる。
/// ゲーム中に誰かが切断したら、ホストでその人の操作役を CPU に代え、CPU の操作をその人の操作として全員へ配る。
/// 部屋主がキックした人は、リタイアさせてから切断するため CPU に代えない。
/// ルーム設定で空き枠を埋めた CPU は、最初からホストで CPU が操作する。
/// ゲームが始まったら、それ以降の参加は断る。
/// </summary>
public class NetworkMatchBootstrap : MonoBehaviour, IPlayerActionChannel
{
    [SerializeField] private PlayerRegistry registry;

    [SerializeField] private TurnFlowController turnFlow;

    [SerializeField] private LocalPlayerAgent localAgent;

    [SerializeField] private GameStartGate startGate;

    [SerializeField] private GameManager game;

    [SerializeField] private PlayerSessionService sessionService;

    [Tooltip("出品・オークションの待ち時間を全員の画面でそろえる時計")]
    [SerializeField] private NetworkPhaseClock phaseClock;

    readonly List<NetworkSessionPlayer> players = new List<NetworkSessionPlayer>();
    readonly Dictionary<ulong, NetworkSessionPlayer> playerByClient = new Dictionary<ulong, NetworkSessionPlayer>();
    readonly Dictionary<int, NetworkSessionPlayer> cpuPlayers = new Dictionary<int, NetworkSessionPlayer>();
    readonly HashSet<int> kickedPlayers = new HashSet<int>();
    NetworkSessionPlayer localPlayer;
    NetworkManager networkManager;

    /// <summary>切断した人の代わりに CPU が操作しているか</summary>
    public bool IsCpu(int playerNumber) => cpuPlayers.ContainsKey(playerNumber);

    /// <summary>ホストのみ：キックした人を、切断しても CPU に代えないよう覚えておく</summary>
    public void MarkKicked(int playerNumber) => kickedPlayers.Add(playerNumber);

    void OnDestroy()
    {
        foreach (NetworkSessionPlayer player in players)
        {
            if (player == null)
                continue;
            player.ActionReceived -= turnFlow.ReceiveAction;
            player.CpuTookOver -= turnFlow.AnnounceCpuTakeover;
        }

        if (networkManager != null)
            networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
    }

    /// <summary>ホストから届いたゲーム開始の通知で、進行を始めてからゲームを開始する</summary>
    public void Begin(int seed)
    {
        if (players.Count > 0)
            return;

        var agents = new List<IPlayerAgent>();
        foreach (int playerNumber in registry.PlayerNumbers)
        {
            if (!registry.TryGet(playerNumber, out Transform body) || !body.TryGetComponent(out NetworkSessionPlayer player))
                continue;

            Track(player);
            if (playerNumber == registry.LocalPlayerNumber)
            {
                localPlayer = player;
                localAgent.Initialize(playerNumber, turnFlow.Commands);
                agents.Add(localAgent);
            }
            else if (player.IsCpu && player.IsServer)
            {
                agents.Add(TakeOverByCpu(player));
            }
            else
            {
                agents.Add(new RemotePlayerAgent(playerNumber));
            }
        }

        if (localPlayer != null && localPlayer.IsServer)
            BeginHosting(localPlayer.NetworkManager);

        phaseClock.Connect(localPlayer, players, IsCpu);
        turnFlow.Activate(agents, this, seed);
        startGate.NotifyStarted();
        Debug.Log($"[Session] {agents.Count} 人でターンの進行を始めました（シード {seed}）");
    }

    /// <summary>
    /// 操作を全員へ配る。自分以外のプレイヤーの操作（CPU が代わりに操作している人・キックした人のリタイア）は、ホストからその人の操作として配る
    /// </summary>
    public void Send(PlayerAction action)
    {
        if (localPlayer == null)
            return;

        if (action.Player == localPlayer.PlayerNumber)
            localPlayer.Send(action);
        else if (localPlayer.IsServer && TryFindPlayer(action.Player, out NetworkSessionPlayer other))
            other.SendByServer(action);
    }

    bool TryFindPlayer(int playerNumber, out NetworkSessionPlayer player)
    {
        player = players.Find(p => p != null && p.PlayerNumber == playerNumber);
        return player != null;
    }

    void Track(NetworkSessionPlayer player)
    {
        players.Add(player);
        if (!player.IsCpu)
            playerByClient[player.OwnerClientId] = player;
        player.ActionReceived += turnFlow.ReceiveAction;
        player.CpuTookOver += turnFlow.AnnounceCpuTakeover;
    }

    /// <summary>ホストのみ：このプレイヤーを CPU が操作し、その操作をこのプレイヤーの操作として全員へ配る</summary>
    CpuPlayerAgent TakeOverByCpu(NetworkSessionPlayer player)
    {
        var cpu = player.gameObject.AddComponent<CpuPlayerAgent>();
        cpu.Initialize(player.PlayerNumber, game, turnFlow.Commands);
        cpuPlayers[player.PlayerNumber] = player;
        return cpu;
    }

    /// <summary>
    /// ホストだけが行う：参加を締め切り、切断しても全員のプレイヤー（体と番号）が残るようにして、切断を見張る。
    /// 切断したプレイヤーの持ち主はホストに移る。
    /// </summary>
    void BeginHosting(NetworkManager manager)
    {
        networkManager = manager;
        sessionService.CloseEntry();
        foreach (NetworkSessionPlayer player in players)
            player.NetworkObject.DontDestroyWithOwner = true;
        networkManager.OnClientDisconnectCallback += HandleClientDisconnected;
    }

    void HandleClientDisconnected(ulong clientId)
    {
        if (!playerByClient.TryGetValue(clientId, out NetworkSessionPlayer player) || player == null || player == localPlayer)
            return;

        playerByClient.Remove(clientId);
        int playerNumber = player.PlayerNumber;
        if (cpuPlayers.ContainsKey(playerNumber) || kickedPlayers.Contains(playerNumber) || !game.IsAlive(playerNumber))
            return;

        turnFlow.ReplaceAgent(TakeOverByCpu(player));
        player.AnnounceCpuTakeover();
        Debug.Log($"[Session] Player{playerNumber} が切断したため、CPU に切り替えました");
    }
}
