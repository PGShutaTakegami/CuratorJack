using System.Collections.Generic;
using EnvironmentSwitcher;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// MultiPlay 画面の部屋選択。人数を選んでメッセージを入力して部屋を作るか、一覧の部屋をダブルクリックして GameScene へ移動する。
/// 一覧には選択中の人数の部屋だけを表示する。
/// </summary>
public class RoomLobbyController : MonoBehaviour
{
    [SerializeField] private RoomCapacitySelector capacitySelector;

    [SerializeField] private Button makeRoomButton;

    [SerializeField] private RoomListView roomListView;

    [SerializeField] private RoomMessagePrompt messagePrompt;

    [Tooltip("部屋に入ったときに読み込むシーン")]
    [SerializeField] private string gameSceneName = "GameScene";

    [Tooltip("部屋一覧を更新する間隔（秒）")]
    [SerializeField] private float refreshInterval = 0.5f;

    IRoomDirectory directory;
    float refreshTimer;
    bool isEnteringRoom;

    void Awake()
    {
        if (EnvironmentRuntime.NetworkMode != EnvironmentNetworkMode.Local)
        {
            Debug.LogWarning($"[Room] 部屋一覧は Local 環境のみ対応です（現在: {EnvironmentRuntime.Current}）");
            enabled = false;
            return;
        }

        directory = new LocalFileRoomDirectory();
    }

    void OnEnable()
    {
        makeRoomButton.onClick.AddListener(MakeRoom);
        capacitySelector.CapacityChanged += HandleCapacityChanged;
        roomListView.RoomDoubleClicked += JoinRoom;
    }

    void OnDisable()
    {
        makeRoomButton.onClick.RemoveListener(MakeRoom);
        capacitySelector.CapacityChanged -= HandleCapacityChanged;
        roomListView.RoomDoubleClicked -= JoinRoom;
    }

    void Start()
    {
        RefreshList();
    }

    void Update()
    {
        refreshTimer += Time.unscaledDeltaTime;
        if (refreshTimer < refreshInterval)
            return;

        refreshTimer = 0f;
        RefreshList();
    }

    void MakeRoom()
    {
        if (isEnteringRoom || messagePrompt.IsOpen)
            return;

        int capacity = capacitySelector.SelectedCapacity;
        if (capacity <= 0)
        {
            Debug.LogWarning("[Room] 先に人数を選択してください");
            return;
        }

        messagePrompt.Open(message => CreateRoom(capacity, message));
    }

    void CreateRoom(int capacity, string message)
    {
        RoomInfo room = LocalRoomFactory.Create(capacity, $"{capacity}人部屋 #{Random.Range(1000, 10000)}", message);
        RoomConnectionContext.SetHost(room);
        EnterRoom($"[Room] 部屋を作成します: {room.roomName}（定員 {capacity} 人, port={room.port}）");
    }

    void JoinRoom(RoomInfo room)
    {
        if (isEnteringRoom || messagePrompt.IsOpen)
            return;

        if (room.IsFull)
        {
            Debug.LogWarning("[Room] 入室できませんでした: 満員です");
            return;
        }

        RoomConnectionContext.SetClient(room);
        EnterRoom($"[Room] 部屋に入ります: {room.roomName}");
    }

    void EnterRoom(string message)
    {
        isEnteringRoom = true;
        Debug.Log(message);
        SceneManager.LoadScene(gameSceneName);
    }

    void HandleCapacityChanged(int capacity)
    {
        RefreshList();
    }

    void RefreshList()
    {
        int capacity = capacitySelector.SelectedCapacity;
        var visibleRooms = new List<RoomInfo>();
        foreach (RoomInfo room in directory.FetchRooms())
        {
            if (room.capacity == capacity)
                visibleRooms.Add(room);
        }

        roomListView.Show(visibleRooms);
    }
}
