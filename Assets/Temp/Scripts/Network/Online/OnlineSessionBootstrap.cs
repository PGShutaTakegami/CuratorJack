using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EnvironmentSwitcher;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// OnlineNet 環境のとき、ロビーで選んだ部屋を Unity Gaming Services のセッション（Lobby + Relay）として開始する。
/// 作った部屋は別の PC のロビーにも表示され、別々のネットワークからでも Relay 経由で接続できる。
/// ゲームが始まったら部屋をロックして一覧から外す。入れなかったとき・ゲーム中にホストとの接続が切れたときはロビーへ戻る。
/// ゲーム開始前にホストが抜けたら、セッションのホストの引き継ぎ（Host Migration）で、残った中で最も先に入った人がホストになる。
/// リトライでは、最初にリトライした人が同じ設定の部屋を作り直してホストになり、後から来た人はそこへ入る。
/// 元の部屋主が戻るまでは作り直した人も部屋主にならず、元の部屋主が入ってきたらホストを渡す。リザルトから一定時間戻らなければ、作り直した人がそのまま部屋主になる。
/// セッションの最大人数は作ったあと変えられないため席の数で作り、部屋の人数はプロパティとして公開する（ルーム設定で変えたら公開し直す）。
/// </summary>
public class OnlineSessionBootstrap : MonoBehaviour, IMatchSession, IRoomSession
{
    [SerializeField] private NetworkManager networkManager;

    [SerializeField] private UnityTransport transport;

    [SerializeField] private PlayerSessionService sessionService;

    [SerializeField] private ReadyRoster readyRoster;

    [SerializeField] private GameStartGate startGate;

    [SerializeField] private PlayerRegistry playerRegistry;

    [SerializeField] private SellerSelector sellerSelector;

    [Tooltip("ルーム設定で変えた部屋の人数を一覧に公開し直し、部屋主待ちを全員へ同期する")]
    [SerializeField] private NetworkRoomState roomState;

    [Tooltip("オンライン時は非表示にする、シーン配置済みのプレイヤー")]
    [SerializeField] private GameObject offlinePlaceholder;

    [Tooltip("部屋に入れなかったときに戻るシーン")]
    [SerializeField] private string lobbySceneName = "HumanMonitorScene";

    [Tooltip("部屋に入ってからホストとの接続が完了するまで待つ秒数")]
    [SerializeField] private float connectTimeoutSeconds = 15f;

    [Tooltip("相手の応答が途絶えてから切断とみなすまでの時間（ミリ秒）。インターネット越しの遅延で誤って切断しないよう Local より長めにする")]
    [SerializeField] private int disconnectTimeoutMS = 10000;

    [Tooltip("作り直した部屋が同時に作られていないかを確かめるまで待つ時間（ミリ秒）。Lobby の問い合わせ回数の上限に掛からないよう空ける")]
    [SerializeField] private int rematchPollMilliseconds = 2000;

    [Tooltip("リザルトから、作り直した部屋で元の部屋主の戻りを待つ秒数")]
    [SerializeField] private float ownerWaitSeconds = 30f;

    [Tooltip("ゲーム開始前にホストとの接続が切れたとき、ホストの引き継ぎで繋ぎ直されるのを待つ秒数")]
    [SerializeField] private float hostMigrationWaitSeconds = 30f;

    ISession session;
    bool isWatchingHost;
    bool isLeaving;
    bool hasSessionEnded;
    int publishedCapacity;
    float migrationDeadline = -1f;
    bool lastOwnerPending;
    bool wantsOwnership;
    bool hasRequestedOwnership;
    RematchOwnerTimer ownerTimer;
    RematchTicket rematchTicket;
    float matchFinishedAt = -1f;

    public bool IsActive { get; private set; }

    async void Start()
    {
        if (EnvironmentRuntime.NetworkMode != EnvironmentNetworkMode.OnlineNet)
            return;

        if (!EnvironmentNetwork.TryBeginRequest("OnlineSession"))
            return;

        IsActive = true;

        if (offlinePlaceholder != null)
            offlinePlaceholder.SetActive(false);

        if (!RoomConnectionContext.HasRoom)
        {
            EnvironmentNetwork.LogError("OnlineNet ではロビーで部屋を選んでから GameScene に入ってください");
            ReturnToLobby();
            return;
        }

        RoomInfo room = RoomConnectionContext.Room;
        bool isHost = RoomConnectionContext.CurrentRole == RoomConnectionContext.Role.Host;
        transport.DisconnectTimeoutMS = disconnectTimeoutMS;
        sessionService.Bind(networkManager, room.capacity);
        readyRoster.SetTotal(room.capacity);
        publishedCapacity = room.capacity;
        startGate.Started += LockSession;
        roomState.Changed += PublishCapacity;
        roomState.OwnershipRequested += HandleOwnershipRequested;
        bool isRematch = RematchContext.TryTake(out rematchTicket);

        try
        {
            await OnlineServices.EnsureSignedInAsync();
            if (isRematch)
                await RematchAsync(room);
            else if (isHost)
                await HostAsync(room);
            else
                await JoinAsync(room.roomId, room);
        }
        catch (Exception exception)
        {
            string action = isRematch ? "リトライ後の部屋に入室" : isHost ? "部屋を作成" : "部屋に入室";
            EnvironmentNetwork.LogError($"{action}できませんでした: {exception.Message}");
            await LeaveAndReturnToLobbyAsync();
        }
    }

    async Task HostAsync(RoomInfo room)
    {
        if (!await CreateSessionAsync(room, null))
            return;

        BecomeRoomHost(false);
        Debug.Log($"[Session] オンラインの部屋を作成しました: {room.roomName}（定員 {room.capacity} 人, id={session.Id}）");
        EnvironmentNetwork.ReportSuccess();
    }

    /// <summary>作り直した部屋のために rematchOf（直前の部屋の ID）を渡すと、目印として部屋に付ける。作れたあとシーンを離れていたら false。</summary>
    async Task<bool> CreateSessionAsync(RoomInfo room, string rematchOf)
    {
        var options = new SessionOptions
        {
            Name = room.roomName,
            MaxPlayers = sessionService.SeatCount,
            IsPrivate = false,
            SessionProperties = new Dictionary<string, SessionProperty>
            {
                [OnlineRoomCatalog.CapacityKey] = new SessionProperty(room.capacity.ToString())
            }
        }.WithRelayNetwork().WithHostMigration(new RoomMigrationDataHandler());

        if (!string.IsNullOrEmpty(room.message))
            options.SessionProperties[OnlineRoomCatalog.MessageKey] = new SessionProperty(room.message);

        if (!string.IsNullOrEmpty(rematchOf))
            options.SessionProperties[OnlineRoomCatalog.RematchKey] = new SessionProperty(rematchOf);

        session = await MultiplayerService.Instance.CreateSessionAsync(options);
        if (await LeaveIfDestroyedAsync())
            return false;

        SubscribeSession(session);
        await WaitUntilAsync(() => networkManager.IsServer && roomState.IsSpawned, "部屋のホストを開始できませんでした");
        if (await LeaveIfDestroyedAsync())
            return false;

        RoomConnectionContext.SetHost(room);
        isWatchingHost = true;
        return true;
    }

    /// <summary>入れたあとシーンを離れていたら false。</summary>
    async Task<bool> JoinAsync(string roomId, RoomInfo room)
    {
        session = await MultiplayerService.Instance.JoinSessionByIdAsync(roomId);
        if (await LeaveIfDestroyedAsync())
            return false;

        SubscribeSession(session);
        await WaitUntilAsync(() => networkManager.IsConnectedClient, null);
        if (await LeaveIfDestroyedAsync())
            return false;

        RoomConnectionContext.SetClient(room);
        isWatchingHost = true;
        Debug.Log($"[Session] オンラインの部屋に入室しました: {room.roomName}");
        EnvironmentNetwork.ReportSuccess();
        return true;
    }

    /// <summary>
    /// 直前の部屋を作り直した部屋があれば入り、無ければ同じ設定で自分が作り直してホストになる。
    /// 作り直した部屋は、自分が元の部屋主でなければ部屋主待ちにする。同時に作られていたら、誰も入っていないうちに先に作られた部屋へ移る。
    /// </summary>
    async Task RematchAsync(RoomInfo room)
    {
        var catalog = new OnlineRoomCatalog();
        string previousRoomId = rematchTicket.PreviousRoomId;
        Debug.Log("[Session] リトライ後の部屋を探しています");

        string roomId = await catalog.FindRematchRoomIdAsync(previousRoomId);
        if (this == null)
            return;

        if (!string.IsNullOrEmpty(roomId))
        {
            await JoinRematchRoomAsync(roomId, room);
            return;
        }

        if (!await CreateSessionAsync(room, previousRoomId))
            return;

        roomState.SetOwnerPending(true);
        await Task.Delay(rematchPollMilliseconds);
        if (await LeaveIfDestroyedAsync())
            return;

        string earliestId = await catalog.FindRematchRoomIdAsync(previousRoomId);
        if (await LeaveIfDestroyedAsync())
            return;

        if (!string.IsNullOrEmpty(earliestId) && earliestId != session.Id && session.PlayerCount <= 1)
        {
            Debug.Log("[Session] 先に作り直された部屋があるため移ります");
            isWatchingHost = false;
            await LeaveSessionAsync();
            await JoinRematchRoomAsync(earliestId, room);
            return;
        }

        BecomeRoomHost(!rematchTicket.WasRoomOwner);
        Debug.Log($"[Session] リトライ後の部屋を作り直しました: {room.roomName}（id={session.Id}）");
        EnvironmentNetwork.ReportSuccess();
    }

    async Task JoinRematchRoomAsync(string roomId, RoomInfo room)
    {
        if (await JoinAsync(roomId, room))
            wantsOwnership = rematchTicket.WasRoomOwner;
    }

    async Task WaitUntilAsync(Func<bool> condition, string timeoutMessage)
    {
        float deadline = Time.realtimeSinceStartup + connectTimeoutSeconds;
        while (this != null && !condition())
        {
            if (Time.realtimeSinceStartup > deadline)
                throw new TimeoutException(timeoutMessage ?? RejectionMessage());

            await Task.Yield();
        }
    }

    string RejectionMessage()
    {
        string reason = networkManager.DisconnectReason;
        if (!string.IsNullOrEmpty(reason) && reason.Contains(PlayerSessionService.SessionFullReason))
            return "満員です";

        if (!string.IsNullOrEmpty(reason) && reason.Contains(PlayerSessionService.GameStartedReason))
            return "ゲームが始まっています";

        return "ホストに接続できませんでした";
    }

    void AllowHostStart()
    {
        new HostGameAuthority(networkManager, startGate, sellerSelector, playerRegistry).Grant();
    }

    /// <summary>ホストとして部屋を持つ。部屋主待ちなら、元の部屋主の戻りを待ってからスタートを許可する。</summary>
    void BecomeRoomHost(bool ownerPending)
    {
        RoomConnectionContext.SetHost(RoomConnectionContext.Room);
        roomState.SetOwnerPending(ownerPending);
        if (!ownerPending)
        {
            ownerTimer = null;
            AllowHostStart();
            return;
        }

        ownerTimer = new RematchOwnerTimer(rematchTicket, 0f, Time.realtimeSinceStartup);
        Debug.Log("[Session] 元の部屋主の戻りを待っています（それまで部屋主はいません）");
    }

    void SubscribeSession(ISession target)
    {
        hasSessionEnded = false;
        target.SessionMigrated += HandleSessionMigrated;
        target.Deleted += HandleSessionEnded;
        target.RemovedFromSession += HandleSessionEnded;
    }

    void UnsubscribeSession(ISession target)
    {
        target.SessionMigrated -= HandleSessionMigrated;
        target.Deleted -= HandleSessionEnded;
        target.RemovedFromSession -= HandleSessionEnded;
    }

    void HandleSessionEnded() => hasSessionEnded = true;

    /// <summary>ホストの引き継ぎで繋ぎ直し終わった。新しいホストになった画面が、部屋主待ちを引き継ぐ。</summary>
    void HandleSessionMigrated()
    {
        if (this == null || session == null || !isWatchingHost)
            return;

        migrationDeadline = -1f;
        if (!session.IsHost)
        {
            ownerTimer = null;
            RoomConnectionContext.SetClient(RoomConnectionContext.Room);
            Debug.Log("[Session] 新しいホストへ繋ぎ直しました");
            return;
        }

        bool hasClaimed = hasRequestedOwnership;
        hasRequestedOwnership = false;
        if (!hasClaimed && TryFindEarlierMember(out string earlierPlayerId))
        {
            roomState.SetOwnerPending(lastOwnerPending);
            TransferHost(earlierPlayerId, "自分より先に入った人へホストを渡します", lastOwnerPending);
            return;
        }

        BecomeRoomHost(!hasClaimed && lastOwnerPending);
        Debug.Log("[Session] 部屋のホストを引き継ぎました");
    }

    /// <summary>ホストは Lobby が選ぶため、残った中で最も先に入った人が自分でなければその人を探す。</summary>
    bool TryFindEarlierMember(out string playerId)
    {
        playerId = null;
        IReadOnlyPlayer self = session.CurrentPlayer;
        if (self == null)
            return false;

        DateTime earliest = self.Joined;
        foreach (IReadOnlyPlayer player in session.Players)
        {
            if (player.Id != self.Id && player.Joined < earliest)
            {
                earliest = player.Joined;
                playerId = player.Id;
            }
        }

        return playerId != null;
    }

    /// <summary>セッションのホストを playerId のプレイヤーへ移す。全員の接続はホストの引き継ぎで繋ぎ直される。移せなければ自分がホストを続ける。</summary>
    async void TransferHost(string playerId, string reason, bool ownerPendingIfFailed)
    {
        Debug.Log($"[Session] {reason}");
        try
        {
            IHostSession hostSession = session.AsHost();
            hostSession.Host = playerId;
            await hostSession.SavePropertiesAsync();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[Session] ホストを渡せなかったため、自分がホストを続けます: {exception.Message}");
            if (this != null && session != null && session.IsHost)
                BecomeRoomHost(ownerPendingIfFailed);
        }
    }

    /// <summary>部屋主待ちの間に元の部屋主が入ってきたら、その画面へホストを渡す。</summary>
    void HandleOwnershipRequested(ulong clientId, string previousRoomId, string playerId)
    {
        if (ownerTimer == null || session == null || !session.IsHost || string.IsNullOrEmpty(playerId) || previousRoomId != RematchOfSession())
            return;

        ownerTimer = null;
        TransferHost(playerId, "元の部屋主が戻ったため、ホストを渡します", false);
    }

    string RematchOfSession()
    {
        return session.Properties != null && session.Properties.TryGetValue(OnlineRoomCatalog.RematchKey, out SessionProperty property)
            ? property.Value
            : null;
    }

    void Update()
    {
        UpdateAwaitingOwner();

        if (!isWatchingHost)
            return;

        if (networkManager.IsConnectedClient)
        {
            migrationDeadline = -1f;
            lastOwnerPending = roomState.IsOwnerPending;
            if (!lastOwnerPending && !networkManager.IsServer)
                hasRequestedOwnership = false;
            TryRequestOwnership();
            return;
        }

        if (TryGetLeaveReason(out string reason))
        {
            isWatchingHost = false;
            EnvironmentNetwork.LogError(reason);
            _ = LeaveAndReturnToLobbyAsync();
            return;
        }

        if (migrationDeadline < 0f)
        {
            migrationDeadline = Time.realtimeSinceStartup + hostMigrationWaitSeconds;
            Debug.Log("[Session] ホストとの接続が切れました。ホストの引き継ぎを待っています");
        }
    }

    /// <summary>キック・ゲーム中の切断・部屋の消滅・引き継ぎの時間切れでは、引き継ぎを待たずにロビーへ戻る。</summary>
    bool TryGetLeaveReason(out string reason)
    {
        reason = null;
        if (PlayerSessionService.WasKicked(networkManager))
            reason = "部屋主にキックされたため、ロビーへ戻ります";
        else if (startGate.HasStarted || session == null || hasSessionEnded)
            reason = "ホストとの接続が切れたため、ロビーへ戻ります";
        else if (migrationDeadline >= 0f && Time.realtimeSinceStartup > migrationDeadline)
            reason = "ホストを引き継げなかったため、ロビーへ戻ります";

        return reason != null;
    }

    /// <summary>元の部屋主が戻らないまま期限を過ぎたら、作り直した画面がそのまま部屋主になる。</summary>
    void UpdateAwaitingOwner()
    {
        if (ownerTimer == null || !ownerTimer.IsExpired(Time.realtimeSinceStartup))
            return;

        ownerTimer = null;
        roomState.SetOwnerPending(false);
        AllowHostStart();
        Debug.Log("[Session] 元の部屋主が戻らなかったため、部屋主になりました");
    }

    /// <summary>元の部屋主として入った部屋が部屋主待ちなら、一度だけ部屋主に戻りたいと申し出る。</summary>
    void TryRequestOwnership()
    {
        if (!wantsOwnership || !roomState.IsSpawned)
            return;

        wantsOwnership = false;
        if (!roomState.IsOwnerPending || session?.CurrentPlayer == null)
            return;

        hasRequestedOwnership = true;
        roomState.RequestOwnership(rematchTicket.PreviousRoomId, session.CurrentPlayer.Id);
        Debug.Log("[Session] 元の部屋主として、部屋主に戻りたいと申し出ました");
    }

    /// <summary>ゲームが始まった部屋は一覧に出さず、新しい参加も受け付けない。</summary>
    async void LockSession()
    {
        if (session == null || !session.IsHost)
            return;

        try
        {
            IHostSession hostSession = session.AsHost();
            hostSession.IsLocked = true;
            await hostSession.SavePropertiesAsync();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[Session] 部屋をロックできませんでした: {exception.Message}");
        }
    }

    /// <summary>ルーム設定で部屋の人数が変わったら、一覧に出す人数を公開し直す。</summary>
    async void PublishCapacity()
    {
        int capacity = roomState.Capacity;
        if (session == null || !session.IsHost || capacity <= 0 || capacity == publishedCapacity)
            return;

        publishedCapacity = capacity;
        try
        {
            IHostSession hostSession = session.AsHost();
            hostSession.SetProperty(OnlineRoomCatalog.CapacityKey, new SessionProperty(capacity.ToString()));
            await hostSession.SavePropertiesAsync();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[Session] 部屋の人数を公開し直せませんでした: {exception.Message}");
        }
    }

    /// <summary>待っている間にシーンを離れていたら、作った / 入った部屋から抜ける。</summary>
    async Task<bool> LeaveIfDestroyedAsync()
    {
        if (this != null)
            return false;

        await LeaveSessionAsync();
        return true;
    }

    async Task LeaveAndReturnToLobbyAsync()
    {
        if (isLeaving)
            return;

        isLeaving = true;
        await LeaveSessionAsync();
        if (this != null)
            ReturnToLobby();
    }

    /// <summary>
    /// ゲーム開始前にホストが抜けるときは、残った人へホストを引き継ぐため部屋から抜けるだけにする。
    /// ゲームが始まった部屋のホストは、引き継がずに部屋ごと削除して一覧に残らないようにする。
    /// </summary>
    async Task LeaveSessionAsync()
    {
        ISession leaving = session;
        session = null;
        if (leaving == null)
            return;

        UnsubscribeSession(leaving);
        bool hasStarted = startGate != null && startGate.HasStarted;
        try
        {
            if (leaving.IsHost && hasStarted)
                await leaving.AsHost().DeleteAsync();
            else
                await leaving.LeaveAsync();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[Session] 部屋から抜けられませんでした: {exception.Message}");
        }
    }

    /// <summary>ゲームが終わったら、接続が切れてもロビーへ戻らずリザルトに残す。リトライ後の部屋主待ちの期限はここから数える。</summary>
    public void FinishMatch()
    {
        StopWatching();
        matchFinishedAt = Time.realtimeSinceStartup;
    }

    /// <summary>今の部屋の ID と、自分が部屋主だったかを目印として残す。ホストは部屋を削除し、クライアントは抜ける。</summary>
    public async Task PrepareRetryAsync()
    {
        if (session != null)
        {
            float finishedAt = matchFinishedAt >= 0f ? matchFinishedAt : Time.realtimeSinceStartup;
            RematchContext.Set(new RematchTicket(session.Id, roomState.IsLocalOwner, finishedAt + ownerWaitSeconds));
        }
        StopWatching();
        isLeaving = true;
        await LeaveSessionAsync();
        DestroyNetwork();
    }

    /// <summary>試合の途中（脱落後）に抜けることもあるため、切断を「ホストとの接続が切れた」と扱わないよう見張りを止めてから抜ける。</summary>
    public async Task PrepareTitleAsync()
    {
        StopWatching();
        isLeaving = true;
        RoomConnectionContext.Clear();
        RematchContext.Clear();
        await LeaveSessionAsync();
        DestroyNetwork();
    }

    /// <summary>自分から抜けるため、切断を「ホストとの接続が切れた」と扱わないよう見張りを止めてから抜ける。</summary>
    public async Task PrepareLeaveRoomAsync()
    {
        StopWatching();
        isLeaving = true;
        RoomConnectionContext.Clear();
        RematchContext.Clear();
        TitleSelectionContext.Set(TitleSelectManager.TitleSelect.MultiPlay);
        await LeaveSessionAsync();
        DestroyNetwork();
    }

    void StopWatching()
    {
        isWatchingHost = false;
        wantsOwnership = false;
        ownerTimer = null;
    }

    /// <summary>NetworkManager はシーンをまたいで残るため、破棄してから MultiPlay 画面へ戻る。</summary>
    void ReturnToLobby()
    {
        StopWatching();
        RoomConnectionContext.Clear();
        RematchContext.Clear();
        TitleSelectionContext.Set(TitleSelectManager.TitleSelect.MultiPlay);
        DestroyNetwork();
        SceneManager.LoadScene(lobbySceneName);
    }

    void DestroyNetwork()
    {
        if (networkManager != null)
            Destroy(networkManager.gameObject);
    }

    void OnDestroy()
    {
        if (startGate != null)
            startGate.Started -= LockSession;
        if (roomState != null)
        {
            roomState.Changed -= PublishCapacity;
            roomState.OwnershipRequested -= HandleOwnershipRequested;
        }

        _ = LeaveSessionAsync();
    }
}
