using System.Collections;
using System.Collections.Generic;
using EnvironmentSwitcher;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Local 環境のとき、同じ PC 内（127.0.0.1）でセッションを開始する。
/// ロビーで部屋を選んで来た場合はその部屋のホスト / クライアントになり、入れなければロビーへ戻る。
/// 部屋を経由しない場合は既存ホストへの参加を先に試し、見つからなければ自分がホストになる。
/// </summary>
public class LocalSessionBootstrap : MonoBehaviour
{
    [SerializeField] private NetworkManager networkManager;

    [SerializeField] private UnityTransport transport;

    [SerializeField] private PlayerSessionService sessionService;

    [SerializeField] private RoomHostPublisher roomPublisher;

    [SerializeField] private ReadyRoster readyRoster;

    [SerializeField] private GameStartGate startGate;

    [SerializeField] private PlayerRegistry playerRegistry;

    [SerializeField] private SellerSelector sellerSelector;

    [Tooltip("オンライン時は非表示にする、シーン配置済みのプレイヤー")]
    [SerializeField] private GameObject offlinePlaceholder;

    [SerializeField] private string address = "127.0.0.1";

    [SerializeField] private ushort port = 7777;

    [Tooltip("既存ホストへの参加を待つ秒数")]
    [SerializeField] private float joinProbeSeconds = 1.5f;

    [SerializeField] private int maxStartAttempts = 3;

    [Tooltip("部屋に入れなかったときに戻るシーン")]
    [SerializeField] private string lobbySceneName = "HumanMonitorScene";

    [Tooltip("ホストが抜けたあと、新しいホストへの引き継ぎを待つ秒数")]
    [SerializeField] private float hostRecoverySeconds = 10f;

    [Tooltip("ホストの引き継ぎで、ホスト開始や再接続をやり直す間隔（秒）")]
    [SerializeField] private float hostRecoveryInterval = 0.5f;

    [Tooltip("相手の応答が途絶えてから切断とみなすまでの時間（ミリ秒）")]
    [SerializeField] private int disconnectTimeoutMS = 3000;

    readonly List<ulong> roomMembers = new List<ulong>();
    ulong localMemberId;
    RoomInfo joinedRoom;
    bool isWatchingHost;

    IEnumerator Start()
    {
        if (EnvironmentRuntime.NetworkMode != EnvironmentNetworkMode.Local)
            yield break;

        if (!EnvironmentNetwork.TryBeginRequest("LocalSession"))
            yield break;

        if (offlinePlaceholder != null)
            offlinePlaceholder.SetActive(false);

        transport.ConnectTimeoutMS = 500;
        transport.MaxConnectAttempts = 2;
        transport.DisconnectTimeoutMS = disconnectTimeoutMS;

        if (RoomConnectionContext.HasRoom)
            yield return RunRoomSession(RoomConnectionContext.CurrentRole, RoomConnectionContext.Room);
        else
            yield return RunAutoSession();
    }

    IEnumerator RunRoomSession(RoomConnectionContext.Role role, RoomInfo room)
    {
        transport.SetConnectionData(room.address, (ushort)room.port);
        sessionService.Bind(networkManager, room.capacity);
        readyRoster.SetTotal(room.capacity);

        if (role == RoomConnectionContext.Role.Host)
        {
            if (networkManager.StartHost())
            {
                BeginHostingRoom(room);
                Debug.Log($"[Session] 部屋のホストとして開始しました: {room.roomName}（{room.address}:{room.port}）");
                EnvironmentNetwork.ReportSuccess();
                yield break;
            }

            EnvironmentNetwork.LogError($"部屋を開始できませんでした: {room.roomName}（{room.address}:{room.port}）");
            yield return ShutdownAndWait();
            ReturnToLobby();
            yield break;
        }

        yield return TryJoin();
        if (networkManager.IsConnectedClient)
        {
            WatchHost(room);
            Debug.Log($"[Session] 部屋に入室しました: {room.roomName}（{room.address}:{room.port}）");
            EnvironmentNetwork.ReportSuccess();
            yield break;
        }

        EnvironmentNetwork.LogError($"部屋に入れませんでした: {(IsRejectedAsFull() ? "満員です" : "接続できませんでした")}");
        ReturnToLobby();
    }

    IEnumerator RunAutoSession()
    {
        transport.SetConnectionData(address, port);
        sessionService.Bind(networkManager);
        readyRoster.SetTotal(sessionService.MaxPlayers);

        // 2 つのインスタンスが同時に起動したときにホスト役がぶつからないようにずらす
        yield return new WaitForSeconds(Random.Range(0f, 0.5f));

        for (int attempt = 0; attempt < maxStartAttempts; attempt++)
        {
            yield return TryJoin();
            if (networkManager.IsConnectedClient)
            {
                Debug.Log($"[Session] クライアントとして参加しました（{address}:{port}）");
                EnvironmentNetwork.ReportSuccess();
                yield break;
            }

            if (IsRejectedAsFull())
            {
                EnvironmentNetwork.LogError($"参加を拒否されました: 満員です（最大 {sessionService.MaxPlayers} 人）");
                yield break;
            }

            if (networkManager.StartHost())
            {
                AllowHostStart();
                Debug.Log($"[Session] ホストとして開始しました（{address}:{port}）");
                EnvironmentNetwork.ReportSuccess();
                yield break;
            }

            yield return ShutdownAndWait();
        }

        EnvironmentNetwork.LogError($"Local セッションを開始できませんでした（{address}:{port}）");
    }

    /// <summary>接続失敗時も DisconnectReason は自動で埋まるため、サーバーが返した満員理由だけを拒否とみなす。</summary>
    bool IsRejectedAsFull()
    {
        string reason = networkManager.DisconnectReason;
        return !string.IsNullOrEmpty(reason) && reason.Contains(PlayerSessionService.SessionFullReason);
    }

    /// <summary>スタートできるのはホストだけ。押されたらホストのプレイヤーから全員へ開始を通知する。</summary>
    void AllowHostStart()
    {
        startGate.AllowLocalStart(() =>
        {
            if (TryGetHostPlayer(out NetworkSessionPlayer player))
                player.AnnounceGameStart();
        });
        AllowHostSelectSeller();
    }

    /// <summary>出品者を決めるのはホストだけ。決めたらホストのプレイヤーから全員へ通知する。</summary>
    void AllowHostSelectSeller()
    {
        var random = new System.Random();
        sellerSelector.AllowLocalSelect(() =>
        {
            if (TryGetHostPlayer(out NetworkSessionPlayer player))
                player.AnnounceSeller(SellerSelectionPolicy.Choose(playerRegistry.PlayerNumbers, random));
        });
    }

    bool TryGetHostPlayer(out NetworkSessionPlayer player)
    {
        player = null;
        NetworkObject hostPlayer = networkManager.LocalClient?.PlayerObject;
        return hostPlayer != null && hostPlayer.TryGetComponent(out player);
    }

    void BeginHostingRoom(RoomInfo room)
    {
        roomPublisher.Begin(room, new LocalFileRoomDirectory());
        AllowHostStart();
    }

    /// <summary>部屋のクライアントとして、ホストが抜けたかどうかを見張り始める。</summary>
    void WatchHost(RoomInfo room)
    {
        joinedRoom = room;
        isWatchingHost = true;
        RecordRoomMembers();
    }

    void Update()
    {
        if (!isWatchingHost)
            return;

        if (networkManager.IsConnectedClient)
        {
            RecordRoomMembers();
            return;
        }

        if (networkManager.ShutdownInProgress)
            return;

        if (networkManager.IsClient)
        {
            networkManager.Shutdown();
            return;
        }

        isWatchingHost = false;
        StartCoroutine(RecoverFromHostLeft());
    }

    /// <summary>切断後はプレイヤーが消えて誰が残っていたか分からなくなるため、接続中に記録しておく。</summary>
    void RecordRoomMembers()
    {
        localMemberId = networkManager.LocalClientId;
        roomMembers.Clear();
        foreach (NetworkObject spawned in networkManager.SpawnManager.SpawnedObjectsList)
        {
            if (spawned.IsPlayerObject)
                roomMembers.Add(spawned.OwnerClientId);
        }
    }

    /// <summary>ホストの次に入ったプレイヤーが同じ部屋でホストを立て直し、他のプレイヤーはそこへ入り直す。</summary>
    IEnumerator RecoverFromHostLeft()
    {
        bool isSuccessor = HostSuccessionPolicy.IsSuccessor(localMemberId, roomMembers, NetworkManager.ServerClientId);
        Debug.Log($"[Session] ホストが部屋から抜けました。{(isSuccessor ? "自分が新しいホストになります" : "新しいホストへ入り直します")}");

        float deadline = Time.realtimeSinceStartup + hostRecoverySeconds;
        while (Time.realtimeSinceStartup < deadline)
        {
            if (isSuccessor)
            {
                if (networkManager.StartHost())
                {
                    RoomConnectionContext.SetHost(joinedRoom);
                    BeginHostingRoom(joinedRoom);
                    Debug.Log($"[Session] 部屋のホストを引き継ぎました: {joinedRoom.roomName}");
                    yield break;
                }

                yield return ShutdownAndWait();
            }
            else
            {
                yield return TryJoin();
                if (networkManager.IsConnectedClient)
                {
                    WatchHost(joinedRoom);
                    Debug.Log($"[Session] 新しいホストの部屋へ入り直しました: {joinedRoom.roomName}");
                    yield break;
                }
            }

            yield return new WaitForSecondsRealtime(hostRecoveryInterval);
        }

        EnvironmentNetwork.LogError($"ホストを引き継げませんでした: {joinedRoom.roomName}");
        ReturnToLobby();
    }

    /// <summary>NetworkManager はシーンをまたいで残るため、破棄してから MultiPlay 画面へ戻る。</summary>
    void ReturnToLobby()
    {
        RoomConnectionContext.Clear();
        TitleSelectionContext.Set(TitleSelectManager.TitleSelect.MultiPlay);
        Destroy(networkManager.gameObject);
        SceneManager.LoadScene(lobbySceneName);
    }

    IEnumerator TryJoin()
    {
        if (!networkManager.StartClient())
            yield break;

        float elapsed = 0f;
        while (elapsed < joinProbeSeconds && !networkManager.IsConnectedClient && networkManager.IsClient)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!networkManager.IsConnectedClient)
            yield return ShutdownAndWait();
    }

    IEnumerator ShutdownAndWait()
    {
        if (networkManager.IsListening || networkManager.IsClient)
            networkManager.Shutdown();

        while (networkManager.ShutdownInProgress)
            yield return null;
    }
}
