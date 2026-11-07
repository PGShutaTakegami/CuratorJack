using System.Collections.Generic;
using EnvironmentSwitcher;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// MultiPlay 画面の部屋選択。作成フォームで設定を決めて部屋を作るか、一覧の部屋をダブルクリックして GameScene へ移動する。
/// 一覧は画面を開いたときと更新ボタンを押したときだけ取得し（通信の節約）、検索条件での絞り込みは取得済みの一覧に対して行う。
/// 上の人数の選択は検索の人数条件と連動する。
/// </summary>
public class RoomLobbyController : MonoBehaviour
{
    [SerializeField] private RoomCapacitySelector capacitySelector;

    [SerializeField] private Button makeRoomButton;

    [SerializeField] private Button searchButton;

    [SerializeField] private Button reloadButton;

    [SerializeField] private RoomListView roomListView;

    [SerializeField] private RoomSearchPanel searchPanel;

    [SerializeField] private RoomCreatePanel createPanel;

    [SerializeField] private RoomPasswordPrompt passwordPrompt;

    [Tooltip("部屋に入ったときに読み込むシーン")]
    [SerializeField] private string gameSceneName = "GameScene";

    [Tooltip("更新ボタンを続けて押せるようになるまでの秒数")]
    [FormerlySerializedAs("refreshInterval")]
    [SerializeField] private float reloadCooldown = 0.5f;

    [Tooltip("OnlineNet で更新ボタンを続けて押せるようになるまでの秒数。Lobby サービスの回数制限を超えないよう長めにする")]
    [FormerlySerializedAs("onlineRefreshInterval")]
    [SerializeField] private float onlineReloadCooldown = 3f;

    IRoomCatalog catalog;
    IReadOnlyList<RoomInfo> fetchedRooms = new List<RoomInfo>();
    RoomSearchFilter filter = RoomSearchFilter.None;
    float activeReloadCooldown;
    float nextReloadTime;
    bool isEnteringRoom;
    bool isFetching;

    bool IsBusy => isEnteringRoom || searchPanel.IsOpen || createPanel.IsOpen || passwordPrompt.IsOpen;

    void Awake()
    {
        switch (EnvironmentRuntime.NetworkMode)
        {
            case EnvironmentNetworkMode.Local:
                catalog = new LocalRoomCatalog();
                activeReloadCooldown = reloadCooldown;
                break;
            case EnvironmentNetworkMode.OnlineNet:
                catalog = new OnlineRoomCatalog();
                activeReloadCooldown = onlineReloadCooldown;
                break;
            default:
                Debug.LogWarning($"[Room] 部屋一覧は Local / OnlineNet 環境のみ対応です（現在: {EnvironmentRuntime.Current}）");
                enabled = false;
                break;
        }
    }

    void OnEnable()
    {
        makeRoomButton.onClick.AddListener(OpenCreatePanel);
        searchButton.onClick.AddListener(OpenSearchPanel);
        reloadButton.onClick.AddListener(Reload);
        capacitySelector.CapacityChanged += HandleCapacityChanged;
        roomListView.RoomDoubleClicked += JoinRoom;
    }

    void OnDisable()
    {
        makeRoomButton.onClick.RemoveListener(OpenCreatePanel);
        searchButton.onClick.RemoveListener(OpenSearchPanel);
        reloadButton.onClick.RemoveListener(Reload);
        capacitySelector.CapacityChanged -= HandleCapacityChanged;
        roomListView.RoomDoubleClicked -= JoinRoom;
    }

    void Start()
    {
        filter = filter.WithCapacity(capacitySelector.SelectedCapacity);
        RefreshList();
    }

    void Update()
    {
        reloadButton.interactable = CanReload;
    }

    bool CanReload => !isFetching && Time.unscaledTime >= nextReloadTime;

    void Reload()
    {
        if (IsBusy || !CanReload)
            return;

        RefreshList();
    }

    void OpenSearchPanel()
    {
        if (IsBusy)
            return;

        searchPanel.Open(filter, ApplyFilter);
    }

    void ApplyFilter(RoomSearchFilter applied)
    {
        filter = applied;
        if (capacitySelector.SelectedCapacity != applied.Capacity)
            capacitySelector.Select(applied.Capacity);

        ShowRooms();
    }

    void OpenCreatePanel()
    {
        if (IsBusy)
            return;

        createPanel.Open(capacitySelector.SelectedCapacity, CreateRoom);
    }

    void CreateRoom(RoomCreationRequest request)
    {
        string roomName = request.RoomName.Length > 0
            ? request.RoomName
            : $"{request.Capacity}人部屋 #{Random.Range(1000, 10000)}";
        RoomInfo room = catalog.CreateRoom(request.Capacity, roomName, request.Message, request.Password);
        RoomConnectionContext.SetHost(room);
        EnterRoom($"[Room] 部屋を作成します: {room.roomName}（定員 {room.capacity} 人{(room.hasPassword ? "・パスワードあり" : string.Empty)}）");
    }

    void JoinRoom(RoomInfo room)
    {
        if (IsBusy)
            return;

        if (room.IsFull)
        {
            Debug.LogWarning("[Room] 入室できませんでした: 満員です");
            return;
        }

        if (room.hasPassword)
        {
            passwordPrompt.Open(room.roomName, password => TryJoinWithPassword(room, password));
            return;
        }

        EnterAsClient(room);
    }

    bool TryJoinWithPassword(RoomInfo room, string password)
    {
        if (isEnteringRoom || !catalog.AcceptsPassword(room, password))
            return false;

        room.password = password;
        EnterAsClient(room);
        return true;
    }

    void EnterAsClient(RoomInfo room)
    {
        RoomConnectionContext.SetClient(room);
        EnterRoom($"[Room] 部屋に入ります: {room.roomName}");
    }

    void EnterRoom(string message)
    {
        isEnteringRoom = true;
        Debug.Log(message);
        SceneFader.LoadScene(gameSceneName);
    }

    /// <summary>人数を切り替えただけでは取得し直さず、前回取得した一覧を絞り込み直す（通信の節約）。</summary>
    void HandleCapacityChanged(int capacity)
    {
        filter = filter.WithCapacity(capacity);
        ShowRooms();
    }

    async void RefreshList()
    {
        if (isFetching)
            return;

        isFetching = true;
        nextReloadTime = Time.unscaledTime + activeReloadCooldown;
        try
        {
            fetchedRooms = await catalog.FetchRoomsAsync();
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"[Room] 部屋一覧を取得できませんでした: {exception.Message}");
            return;
        }
        finally
        {
            isFetching = false;
        }

        if (this != null)
            ShowRooms();
    }

    void ShowRooms()
    {
        roomListView.Show(filter.Apply(fetchedRooms));
    }
}
