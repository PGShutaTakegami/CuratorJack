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
/// リトライでは、ホストは同じ名前の部屋を作り直し、クライアントは作り直された部屋を探して入る。
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

    [Tooltip("ルーム設定で変えた部屋の人数を、一覧に公開し直すために見る")]
    [SerializeField] private NetworkRoomState roomState;

    [Tooltip("オンライン時は非表示にする、シーン配置済みのプレイヤー")]
    [SerializeField] private GameObject offlinePlaceholder;

    [Tooltip("部屋に入れなかったときに戻るシーン")]
    [SerializeField] private string lobbySceneName = "HumanMonitorScene";

    [Tooltip("部屋に入ってからホストとの接続が完了するまで待つ秒数")]
    [SerializeField] private float connectTimeoutSeconds = 15f;

    [Tooltip("相手の応答が途絶えてから切断とみなすまでの時間（ミリ秒）。インターネット越しの遅延で誤って切断しないよう Local より長めにする")]
    [SerializeField] private int disconnectTimeoutMS = 10000;

    [Tooltip("リトライしたクライアントが、ホストが部屋を作り直すのを待つ秒数")]
    [SerializeField] private float rematchWaitSeconds = 60f;

    [Tooltip("作り直された部屋を探し直す間隔（ミリ秒）。Lobby の問い合わせ回数の上限に掛からないよう空ける")]
    [SerializeField] private int rematchPollMilliseconds = 2000;

    ISession session;
    bool isWatchingHost;
    bool isLeaving;
    int publishedCapacity;

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

        try
        {
            await OnlineServices.EnsureSignedInAsync();
            if (isHost)
                await HostAsync(room);
            else
                await JoinAsync(room);
        }
        catch (Exception exception)
        {
            EnvironmentNetwork.LogError($"部屋を{(isHost ? "作成" : "入室")}できませんでした: {exception.Message}");
            await LeaveAndReturnToLobbyAsync();
        }
    }

    async Task HostAsync(RoomInfo room)
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
        }.WithRelayNetwork();

        if (!string.IsNullOrEmpty(room.message))
            options.SessionProperties[OnlineRoomCatalog.MessageKey] = new SessionProperty(room.message);

        if (RematchContext.TryTake(out string previousRoomId))
            options.SessionProperties[OnlineRoomCatalog.RematchKey] = new SessionProperty(previousRoomId);

        session = await MultiplayerService.Instance.CreateSessionAsync(options);
        if (await LeaveIfDestroyedAsync())
            return;

        new HostGameAuthority(networkManager, startGate, sellerSelector, playerRegistry).Grant();
        startGate.Started += LockSession;
        publishedCapacity = room.capacity;
        roomState.Changed += PublishCapacity;
        Debug.Log($"[Session] オンラインの部屋を作成しました: {room.roomName}（定員 {room.capacity} 人, id={session.Id}）");
        EnvironmentNetwork.ReportSuccess();
    }

    async Task JoinAsync(RoomInfo room)
    {
        string roomId = RematchContext.TryTake(out string previousRoomId)
            ? await WaitForRematchRoomAsync(previousRoomId)
            : room.roomId;
        if (this == null)
            return;

        session = await MultiplayerService.Instance.JoinSessionByIdAsync(roomId);
        if (await LeaveIfDestroyedAsync())
            return;

        float deadline = Time.realtimeSinceStartup + connectTimeoutSeconds;
        while (!networkManager.IsConnectedClient)
        {
            if (Time.realtimeSinceStartup > deadline)
                throw new TimeoutException(RejectionMessage());

            await Task.Yield();
            if (await LeaveIfDestroyedAsync())
                return;
        }

        isWatchingHost = true;
        Debug.Log($"[Session] オンラインの部屋に入室しました: {room.roomName}");
        EnvironmentNetwork.ReportSuccess();
    }

    /// <summary>ホストより先にリトライしたときは、ホストが部屋を作り直すまで待つ。</summary>
    async Task<string> WaitForRematchRoomAsync(string previousRoomId)
    {
        var catalog = new OnlineRoomCatalog();
        float deadline = Time.realtimeSinceStartup + rematchWaitSeconds;
        Debug.Log("[Session] ホストが部屋を作り直すのを待っています");
        while (true)
        {
            string roomId = await catalog.FindRematchRoomIdAsync(previousRoomId);
            if (!string.IsNullOrEmpty(roomId) || this == null)
                return roomId;

            if (Time.realtimeSinceStartup > deadline)
                throw new TimeoutException("ホストが部屋を作り直しませんでした");

            await Task.Delay(rematchPollMilliseconds);
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

    void Update()
    {
        if (!isWatchingHost || networkManager.IsConnectedClient)
            return;

        isWatchingHost = false;
        EnvironmentNetwork.LogError(PlayerSessionService.WasKicked(networkManager)
            ? "部屋主にキックされたため、ロビーへ戻ります"
            : "ホストとの接続が切れたため、ロビーへ戻ります");
        _ = LeaveAndReturnToLobbyAsync();
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

    /// <summary>ホストが抜けるときは部屋ごと削除し、一覧に残らないようにする。</summary>
    async Task LeaveSessionAsync()
    {
        ISession leaving = session;
        session = null;
        if (leaving == null)
            return;

        try
        {
            if (leaving.IsHost)
                await leaving.AsHost().DeleteAsync();
            else
                await leaving.LeaveAsync();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[Session] 部屋から抜けられませんでした: {exception.Message}");
        }
    }

    public void FinishMatch()
    {
        isWatchingHost = false;
    }

    /// <summary>今の部屋の ID を目印として残す。ホストは部屋を削除し、クライアントは抜ける。</summary>
    public async Task PrepareRetryAsync()
    {
        if (session != null)
            RematchContext.Set(session.Id);
        await LeaveSessionAsync();
        DestroyNetwork();
    }

    public async Task PrepareTitleAsync()
    {
        RoomConnectionContext.Clear();
        RematchContext.Clear();
        await LeaveSessionAsync();
        DestroyNetwork();
    }

    /// <summary>自分から抜けるため、切断を「ホストとの接続が切れた」と扱わないよう見張りを止めてから抜ける。</summary>
    public async Task PrepareLeaveRoomAsync()
    {
        isWatchingHost = false;
        isLeaving = true;
        RoomConnectionContext.Clear();
        RematchContext.Clear();
        TitleSelectionContext.Set(TitleSelectManager.TitleSelect.MultiPlay);
        await LeaveSessionAsync();
        DestroyNetwork();
    }

    /// <summary>NetworkManager はシーンをまたいで残るため、破棄してから MultiPlay 画面へ戻る。</summary>
    void ReturnToLobby()
    {
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
            roomState.Changed -= PublishCapacity;

        _ = LeaveSessionAsync();
    }
}
