using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using EnvironmentSwitcher;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Local 環境のとき、同じ PC 内（127.0.0.1）でセッションを開始する。
/// ロビーで部屋を選んで来た場合はその部屋のホスト / クライアントになり、入れなければロビーへ戻る。
/// 部屋を経由しない場合は既存ホストへの参加を先に試し、見つからなければ自分がホストになる。
/// リトライでは、最初にリトライした人が同じ設定の部屋を作り直してホストになり、後から来た人はそこへ入る。
/// 元の部屋主が戻るまでは作り直した人も部屋主にならず、元の部屋主が入ってきたらホストを渡す。
/// 元の部屋主がタイトルへ戻るか、リザルトから一定時間戻らなければ、作り直した人がそのまま部屋主になる。
/// 部屋のホストが抜けたら、残った中で最も先に入った人がホストを引き継ぐ。部屋主にキックされたときは、ホストの引き継ぎをせずにロビーへ戻る。
/// </summary>
public class LocalSessionBootstrap : MonoBehaviour, IMatchSession, IRoomSession
{
    [SerializeField] private NetworkManager networkManager;

    [SerializeField] private UnityTransport transport;

    [SerializeField] private PlayerSessionService sessionService;

    [SerializeField] private RoomHostPublisher roomPublisher;

    [SerializeField] private NetworkRoomState roomState;

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

    [Tooltip("ホストの引き継ぎで、ホスト開始や再接続をやり直す間隔（秒）。作り直した部屋の見張りもこの間隔で行う")]
    [SerializeField] private float hostRecoveryInterval = 0.5f;

    [Tooltip("相手の応答が途絶えてから切断とみなすまでの時間（ミリ秒）")]
    [SerializeField] private int disconnectTimeoutMS = 3000;

    [Tooltip("リトライ後の部屋を探したり作ったりし続ける秒数")]
    [SerializeField] private float rematchWaitSeconds = 60f;

    [Tooltip("リザルトから、作り直した部屋で元の部屋主の戻りを待つ秒数")]
    [SerializeField] private float ownerWaitSeconds = 30f;

    [Tooltip("元の部屋が閉じてから（元の部屋主がタイトルへ戻った / リトライした）、元の部屋主が入ってくるのを待つ秒数")]
    [SerializeField] private float closedOwnerGraceSeconds = 10f;

    [Tooltip("元の部屋主へホストを渡すとき、次のホストを知らせてから抜けるまでの秒数")]
    [SerializeField] private float handOverDelaySeconds = 0.3f;

    readonly List<ulong> roomMembers = new List<ulong>();
    readonly IRoomDirectory directory = new LocalFileRoomDirectory();
    ulong localMemberId;
    bool lastOwnerPending;
    ulong? nextHostId;
    RoomInfo joinedRoom;
    bool isWatchingHost;
    bool wantsOwnership;
    RoomInfo hostedRoom;
    RematchOwnerTimer ownerTimer;
    RematchTicket rematchTicket;
    float hostingPollTimer;
    float matchFinishedAt = -1f;

    public bool IsActive { get; private set; }

    IEnumerator Start()
    {
        if (EnvironmentRuntime.NetworkMode != EnvironmentNetworkMode.Local)
            yield break;

        if (!EnvironmentNetwork.TryBeginRequest("LocalSession"))
            yield break;

        IsActive = true;
        roomState.OwnershipRequested += HandleOwnershipRequested;
        roomState.NextHostAnnounced += HandleNextHostAnnounced;

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

    void OnDestroy()
    {
        if (roomState == null)
            return;

        roomState.OwnershipRequested -= HandleOwnershipRequested;
        roomState.NextHostAnnounced -= HandleNextHostAnnounced;
    }

    IEnumerator RunRoomSession(RoomConnectionContext.Role role, RoomInfo room)
    {
        sessionService.Bind(networkManager, room.capacity);
        readyRoster.SetTotal(room.capacity);

        if (RematchContext.TryTake(out rematchTicket))
        {
            yield return RunRematch(room, rematchTicket.PreviousRoomId);
            yield break;
        }

        transport.SetConnectionData(room.address, (ushort)room.port);
        if (role == RoomConnectionContext.Role.Host)
        {
            if (networkManager.StartHost())
            {
                BecomeRoomHost(room, false);
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

    /// <summary>
    /// 直前の部屋（previousRoomId）を作り直した部屋があれば入り、無ければ settings と同じ設定で自分が作り直してホストになる。
    /// 元の部屋主だった画面は、入った部屋で部屋主に戻りたいと申し出る。作り直した画面は、元の部屋主でなければ部屋主待ちにする。
    /// </summary>
    IEnumerator RunRematch(RoomInfo settings, string previousRoomId)
    {
        Debug.Log("[Session] リトライ後の部屋を探しています");
        bool wasRoomOwner = rematchTicket != null && rematchTicket.WasRoomOwner;
        float deadline = Time.realtimeSinceStartup + rematchWaitSeconds;
        while (Time.realtimeSinceStartup < deadline)
        {
            RoomInfo target = LocalRematchRooms.FindEarliest(directory, previousRoomId);
            if (target != null)
            {
                transport.SetConnectionData(target.address, (ushort)target.port);
                yield return TryJoin();
                if (networkManager.IsConnectedClient)
                {
                    RoomConnectionContext.SetClient(target);
                    WatchHost(target);
                    wantsOwnership = wasRoomOwner;
                    Debug.Log($"[Session] リトライ後の部屋に入室しました: {target.roomName}（{target.address}:{target.port}）");
                    EnvironmentNetwork.ReportSuccess();
                    yield break;
                }

                if (IsRejectedAsFull())
                    break;
            }
            else
            {
                RoomInfo created = LocalRoomFactory.CreateRematch(settings, previousRoomId);
                transport.SetConnectionData(created.address, (ushort)created.port);
                if (networkManager.StartHost())
                {
                    BecomeRoomHost(created, !wasRoomOwner);
                    Debug.Log($"[Session] リトライ後の部屋を作り直しました: {created.roomName}（{created.address}:{created.port}）");
                    EnvironmentNetwork.ReportSuccess();
                    yield break;
                }

                yield return ShutdownAndWait();
            }

            yield return new WaitForSecondsRealtime(hostRecoveryInterval);
        }

        EnvironmentNetwork.LogError($"リトライ後の部屋に入れませんでした: {(IsRejectedAsFull() ? "満員です" : "部屋を用意できませんでした")}");
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

    void AllowHostStart()
    {
        new HostGameAuthority(networkManager, startGate, sellerSelector, playerRegistry).Grant();
    }

    /// <summary>ホストとして部屋を公開する。部屋主待ちなら、元の部屋主の戻りを待ってからスタートを許可する。</summary>
    void BecomeRoomHost(RoomInfo room, bool ownerPending)
    {
        hostedRoom = room;
        hostingPollTimer = 0f;
        RoomConnectionContext.SetHost(room);
        roomPublisher.Begin(room, directory);
        roomState.SetOwnerPending(ownerPending);

        if (!ownerPending)
        {
            AllowHostStart();
            return;
        }

        ownerTimer = new RematchOwnerTimer(rematchTicket, closedOwnerGraceSeconds, Time.realtimeSinceStartup);
        Debug.Log("[Session] 元の部屋主の戻りを待っています（それまで部屋主はいません）");
    }

    void StopHosting()
    {
        hostedRoom = null;
        ownerTimer = null;
        roomPublisher.End();
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
        if (hostedRoom != null)
            UpdateHosting();

        if (isWatchingHost)
            UpdateWatchingHost();
    }

    /// <summary>ゲーム開始前だけ、同時に作られたリトライ後の部屋をまとめ、部屋主待ちの期限を見る。</summary>
    void UpdateHosting()
    {
        if (!networkManager.IsServer || startGate.HasStarted)
            return;

        hostingPollTimer += Time.unscaledDeltaTime;
        if (hostingPollTimer < hostRecoveryInterval)
            return;

        hostingPollTimer = 0f;
        if (TryFindEarlierRematchRoom(out RoomInfo earlier))
        {
            Debug.Log($"[Session] 先に作り直された部屋があるため移ります: {earlier.roomName}");
            StartCoroutine(MoveToEarlierRematchRoom());
            return;
        }

        UpdateAwaitingOwner();
    }

    /// <summary>まだ誰も入っていないときだけ移る（入った人を置き去りにしないため）。</summary>
    bool TryFindEarlierRematchRoom(out RoomInfo earlier)
    {
        earlier = null;
        if (string.IsNullOrEmpty(hostedRoom.rematchOf) || networkManager.ConnectedClientsIds.Count > 1)
            return false;

        earlier = LocalRematchRooms.FindEarliest(directory, hostedRoom.rematchOf);
        return earlier != null && earlier.roomId != hostedRoom.roomId;
    }

    IEnumerator MoveToEarlierRematchRoom()
    {
        RoomInfo settings = hostedRoom;
        StopHosting();
        yield return ShutdownAndWait();
        yield return RunRematch(settings, settings.rematchOf);
    }

    void UpdateAwaitingOwner()
    {
        if (ownerTimer == null)
            return;

        float now = Time.realtimeSinceStartup;
        if (!ownerTimer.HasPreviousRoomClosed && !LocalRematchRooms.Exists(directory, hostedRoom.rematchOf))
            ownerTimer.NotifyPreviousRoomClosed(now);

        if (ownerTimer.IsExpired(now))
            ConfirmOwner();
    }

    void ConfirmOwner()
    {
        ownerTimer = null;
        roomState.SetOwnerPending(false);
        AllowHostStart();
        Debug.Log("[Session] 元の部屋主が戻らなかったため、部屋主になりました");
    }

    /// <summary>部屋主待ちの間に元の部屋主が入ってきたら、その画面へホストを渡す。</summary>
    void HandleOwnershipRequested(ulong clientId, string previousRoomId, string playerId)
    {
        if (ownerTimer == null || hostedRoom == null || previousRoomId != hostedRoom.rematchOf)
            return;

        ownerTimer = null;
        StartCoroutine(HandOverHost(clientId));
    }

    void HandleNextHostAnnounced(ulong clientId)
    {
        if (!networkManager.IsServer)
            nextHostId = clientId;
    }

    /// <summary>次のホストを全員へ知らせてから抜け、知らされた画面がホストを立て直したらクライアントとして入り直す。</summary>
    IEnumerator HandOverHost(ulong nextHostClientId)
    {
        Debug.Log("[Session] 元の部屋主が戻ったため、ホストを渡します");
        roomState.AnnounceNextHost(nextHostClientId);
        yield return new WaitForSecondsRealtime(handOverDelaySeconds);

        RoomInfo room = hostedRoom;
        StopHosting();
        yield return ShutdownAndWait();
        RoomConnectionContext.SetClient(room);
        joinedRoom = room;
        yield return RecoverFromHostLeft(false, false);
    }

    void UpdateWatchingHost()
    {
        if (networkManager.IsConnectedClient)
        {
            RecordRoomMembers();
            TryRequestOwnership();
            return;
        }

        if (networkManager.ShutdownInProgress)
            return;

        if (PlayerSessionService.WasKicked(networkManager))
        {
            isWatchingHost = false;
            EnvironmentNetwork.LogError("部屋主にキックされたため、ロビーへ戻ります");
            ReturnToLobby();
            return;
        }

        if (networkManager.IsClient)
        {
            networkManager.Shutdown();
            return;
        }

        isWatchingHost = false;
        bool isDesignated = nextHostId.HasValue;
        bool isSuccessor = isDesignated
            ? nextHostId.Value == localMemberId
            : HostSuccessionPolicy.IsSuccessor(localMemberId, roomMembers, NetworkManager.ServerClientId);
        bool ownerPending = !isDesignated && lastOwnerPending;
        nextHostId = null;
        StartCoroutine(RecoverFromHostLeft(isSuccessor, ownerPending));
    }

    /// <summary>元の部屋主として入った部屋が部屋主待ちなら、一度だけ部屋主に戻りたいと申し出る。</summary>
    void TryRequestOwnership()
    {
        if (!wantsOwnership || !roomState.IsSpawned)
            return;

        wantsOwnership = false;
        if (!roomState.IsOwnerPending)
            return;

        roomState.RequestOwnership(joinedRoom.rematchOf, string.Empty);
        Debug.Log("[Session] 元の部屋主として、部屋主に戻りたいと申し出ました");
    }

    /// <summary>切断後はプレイヤーが消えて誰が残っていたか分からなくなるため、接続中に記録しておく。</summary>
    void RecordRoomMembers()
    {
        localMemberId = networkManager.LocalClientId;
        lastOwnerPending = roomState.IsOwnerPending;
        roomMembers.Clear();
        foreach (NetworkObject spawned in networkManager.SpawnManager.SpawnedObjectsList)
        {
            if (spawned.IsPlayerObject)
                roomMembers.Add(spawned.OwnerClientId);
        }
    }

    /// <summary>後継（知らされた次のホスト、いなければホストの次に入ったプレイヤー）が同じ部屋でホストを立て直し、他のプレイヤーはそこへ入り直す。</summary>
    IEnumerator RecoverFromHostLeft(bool isSuccessor, bool ownerPending)
    {
        Debug.Log($"[Session] ホストが部屋から抜けました。{(isSuccessor ? "自分が新しいホストになります" : "新しいホストへ入り直します")}");

        float deadline = Time.realtimeSinceStartup + hostRecoverySeconds;
        while (Time.realtimeSinceStartup < deadline)
        {
            if (isSuccessor)
            {
                if (networkManager.StartHost())
                {
                    BecomeRoomHost(joinedRoom, ownerPending);
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

    /// <summary>ゲームが終わったら、ホストが抜けても引き継がずリザルトに残す。リトライ後の部屋主待ちの期限はここから数える。</summary>
    public void FinishMatch()
    {
        StopWatching();
        matchFinishedAt = Time.realtimeSinceStartup;
    }

    /// <summary>部屋の ID と、自分が部屋主だったかを目印に残す。</summary>
    public Task PrepareRetryAsync()
    {
        if (RoomConnectionContext.HasRoom)
        {
            float finishedAt = matchFinishedAt >= 0f ? matchFinishedAt : Time.realtimeSinceStartup;
            RematchContext.Set(new RematchTicket(RoomConnectionContext.Room.roomId, roomState.IsLocalOwner, finishedAt + ownerWaitSeconds));
        }
        StopWatching();
        Destroy(networkManager.gameObject);
        return Task.CompletedTask;
    }

    /// <summary>試合の途中（脱落後）に抜けることもあるため、切断でホストの引き継ぎが始まらないよう見張りを止めてから抜ける。</summary>
    public Task PrepareTitleAsync()
    {
        StopWatching();
        RoomConnectionContext.Clear();
        RematchContext.Clear();
        Destroy(networkManager.gameObject);
        return Task.CompletedTask;
    }

    /// <summary>自分から抜けるため、切断でホストの引き継ぎが始まらないよう見張りを止めてから抜ける。</summary>
    public Task PrepareLeaveRoomAsync()
    {
        StopWatching();
        RoomConnectionContext.Clear();
        RematchContext.Clear();
        TitleSelectionContext.Set(TitleSelectManager.TitleSelect.MultiPlay);
        Destroy(networkManager.gameObject);
        return Task.CompletedTask;
    }

    void StopWatching()
    {
        isWatchingHost = false;
        wantsOwnership = false;
        nextHostId = null;
        hostedRoom = null;
        ownerTimer = null;
        StopAllCoroutines();
    }

    /// <summary>NetworkManager はシーンをまたいで残るため、破棄してから MultiPlay 画面へ戻る。</summary>
    void ReturnToLobby()
    {
        StopWatching();
        RoomConnectionContext.Clear();
        RematchContext.Clear();
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
