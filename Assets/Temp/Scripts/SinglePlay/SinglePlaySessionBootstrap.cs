using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// SinglePlay から来たとき、ネットワークを使わずにプレイヤーを配置する。
/// シーンに置かれたプレイヤーを自分（Player1）とし、残りの人数分のコンピューターを席に座らせる。
/// SinglePlay を経由しない場合は何もしない。リトライでは同じ人数でスタート前からやり直す。
/// ルーム設定では、スタート前にコンピューターを外して空き枠にしたり、空き枠をコンピューターで埋めたり、人数を変えたりできる。
/// 待ち時間は無制限で固定。
/// </summary>
public class SinglePlaySessionBootstrap : MonoBehaviour, IMatchSession, IRoomControl
{
    const int MinCapacity = 2;

    [SerializeField] private SeatRegistry seatRegistry;

    [Tooltip("SinglePlay 中は無効にする、ネットワーク接続のオブジェクト")]
    [SerializeField] private GameObject networkSession;

    [Tooltip("シーンに置かれた自分のプレイヤー")]
    [SerializeField] private GameObject localPlayer;

    [Tooltip("コンピューターとして生成するプレイヤー")]
    [SerializeField] private GameObject cpuPrefab;

    [SerializeField] private ReadyRoster readyRoster;

    [SerializeField] private GameStartGate startGate;

    [SerializeField] private PlayerRegistry playerRegistry;

    [SerializeField] private SellerSelector sellerSelector;

    [SerializeField] private GameManager game;

    [Tooltip("SinglePlay では、自分とコンピューターの操作役を渡して進行を始める")]
    [SerializeField] private TurnFlowController turnFlow;

    [SerializeField] private LocalPlayerAgent localAgent;

    [Tooltip("SinglePlay では準備の操作が不要なため隠すボタン")]
    [SerializeField] private GameObject readyButton;

    [Tooltip("SinglePlay では隠した準備ボタンの位置へ移すスタートボタン")]
    [SerializeField] private RectTransform startButton;

    readonly List<SinglePlayPlayer> players = new List<SinglePlayPlayer>();
    readonly Dictionary<int, int> seatByPlayer = new Dictionary<int, int>();
    readonly List<RoomMember> members = new List<RoomMember>();
    SeatAssignmentPolicy seatPolicy;
    int totalPlayers;
    int waitSeconds = RoomWaitTime.Infinite;

    public IReadOnlyList<SinglePlayPlayer> Players => players;

    public bool IsActive => totalPlayers > 0;

    public int Capacity => totalPlayers;

    public int MaxCapacity => seatRegistry.SeatCount;

    public IReadOnlyList<RoomMember> Members => members;

    public int WaitSeconds => waitSeconds;

    public bool CanChangeWaitTime => false;

    public bool CanFillWithCpu => true;

    /// <summary>シングルプレイは VC に対応しない</summary>
    public bool IsVoiceChatOn => false;

    public bool CanChangeVoiceChat => false;

    public event System.Action Changed;

    void Awake()
    {
        if (!SinglePlayContext.IsActive)
            return;

        totalPlayers = Mathf.Min(SinglePlayContext.TotalPlayers, seatRegistry.SeatCount);
        SinglePlayContext.Clear();

        if (networkSession != null)
            networkSession.SetActive(false);

        PlacePlayers(totalPlayers);
        RegisterReadyRoster(totalPlayers);
        startGate.AllowLocalStart(startGate.NotifyStarted);
        AllowSelectSeller();
        ActivateTurnFlow();
        Debug.Log($"[SinglePlay] 自分 + コンピューター {totalPlayers - 1} 人で開始しました");
    }

    public void FinishMatch()
    {
    }

    public Task PrepareRetryAsync()
    {
        SinglePlayContext.Set(totalPlayers);
        DestroyNetworkSession();
        return Task.CompletedTask;
    }

    public Task PrepareTitleAsync()
    {
        DestroyNetworkSession();
        return Task.CompletedTask;
    }

    /// <summary>NetworkManager は無効にしていてもシーンをまたいで残ることがあるため、破棄してから遷移する。</summary>
    void DestroyNetworkSession()
    {
        if (networkSession != null)
            Destroy(networkSession);
    }

    public bool SetCapacity(int capacity)
    {
        if (startGate.HasStarted || capacity < Mathf.Max(MinCapacity, players.Count) || capacity > MaxCapacity)
            return false;

        totalPlayers = capacity;
        readyRoster.SetTotal(capacity);
        Changed?.Invoke();
        return true;
    }

    public void SetWaitSeconds(int seconds)
    {
    }

    public void SetVoiceChat(bool on)
    {
    }

    public bool FillWithCpu()
    {
        if (startGate.HasStarted || players.Count >= totalPlayers)
            return false;

        int playerNumber = LowestFreePlayerNumber();
        if (!TryPlace(playerNumber, isCpu: true, out SinglePlayPlayer cpu))
            return false;

        readyRoster.SetReady(cpu.PlayerNumber, true);
        ActivateTurnFlow();
        Changed?.Invoke();
        return true;
    }

    public void Kick(int playerNumber)
    {
        SinglePlayPlayer player = players.Find(p => p.PlayerNumber == playerNumber);
        if (player == null || !player.IsCpu)
            return;

        if (startGate.HasStarted)
        {
            turnFlow.Commands.Retire(playerNumber);
            Changed?.Invoke();
            return;
        }

        players.Remove(player);
        seatByPlayer.Remove(playerNumber);
        playerRegistry.Unregister(playerNumber, player.transform);
        readyRoster.Remove(playerNumber);
        Destroy(player.gameObject);
        RefreshMembers();
        ActivateTurnFlow();
        Changed?.Invoke();
    }

    int LowestFreePlayerNumber()
    {
        int playerNumber = 1;
        while (seatByPlayer.ContainsKey(playerNumber))
            playerNumber++;
        return playerNumber;
    }

    void PlacePlayers(int totalPlayers)
    {
        seatPolicy = new SeatAssignmentPolicy(seatRegistry.SeatCount, seatRegistry.FirstSeat, new System.Random());

        for (int playerNumber = 1; playerNumber <= totalPlayers; playerNumber++)
        {
            if (!TryPlace(playerNumber, isCpu: playerNumber != 1, out _))
                break;
        }
    }

    bool TryPlace(int playerNumber, bool isCpu, out SinglePlayPlayer player)
    {
        player = null;
        int seat = seatPolicy.ChooseSeat(playerNumber, seatByPlayer);
        if (seat == SeatAssignmentPolicy.NoSeat)
            return false;

        seatByPlayer[playerNumber] = seat;
        GameObject playerObject = isCpu ? CreateCpu(playerNumber) : localPlayer;
        Sit(playerObject, seat);

        player = playerObject.GetComponent<SinglePlayPlayer>();
        if (player == null)
            player = playerObject.AddComponent<SinglePlayPlayer>();
        player.Initialize(playerNumber, isCpu);
        players.Add(player);
        playerRegistry.Register(playerNumber, playerObject.transform);
        if (!isCpu)
            playerRegistry.SetLocalPlayer(playerNumber);
        RefreshMembers();
        return true;
    }

    void RefreshMembers()
    {
        members.Clear();
        foreach (SinglePlayPlayer player in players)
            members.Add(new RoomMember(player.PlayerNumber, player.IsCpu, !player.IsCpu));
        members.Sort((a, b) => a.PlayerNumber.CompareTo(b.PlayerNumber));
    }

    /// <summary>SinglePlay は自分の画面で出品者を決める。</summary>
    void AllowSelectSeller()
    {
        var random = new System.Random();
        sellerSelector.AllowLocalSelect(() =>
            sellerSelector.NotifySellerDecided(SellerSelectionPolicy.Choose(playerRegistry.PlayerNumbers, random)));
    }

    void ActivateTurnFlow()
    {
        var agents = new List<IPlayerAgent>();
        foreach (SinglePlayPlayer player in players)
        {
            if (player.IsCpu)
            {
                var cpu = player.GetComponent<CpuPlayerAgent>();
                if (cpu == null)
                    cpu = player.gameObject.AddComponent<CpuPlayerAgent>();
                cpu.Initialize(player.PlayerNumber, game, turnFlow.Commands);
                agents.Add(cpu);
            }
            else
            {
                localAgent.Initialize(player.PlayerNumber, turnFlow.Commands);
                agents.Add(localAgent);
            }
        }
        turnFlow.Activate(agents);
    }

    /// <summary>SinglePlay は自分もコンピューターも最初から準備完了とし、すぐにスタートできるようにする。</summary>
    void RegisterReadyRoster(int totalPlayers)
    {
        readyRoster.SetTotal(totalPlayers);
        foreach (SinglePlayPlayer player in players)
            readyRoster.SetReady(player.PlayerNumber, true);

        ReplaceReadyButtonWithStart();
    }

    void ReplaceReadyButtonWithStart()
    {
        if (readyButton == null)
            return;

        if (startButton != null)
        {
            var readyRect = (RectTransform)readyButton.transform;
            startButton.anchorMin = readyRect.anchorMin;
            startButton.anchorMax = readyRect.anchorMax;
            startButton.anchoredPosition = readyRect.anchoredPosition;
        }

        readyButton.SetActive(false);
    }

    GameObject CreateCpu(int playerNumber)
    {
        GameObject cpu = Instantiate(cpuPrefab);
        cpu.name = $"CPU_Player{playerNumber}";
        foreach (Camera cam in cpu.GetComponentsInChildren<Camera>(true))
            cam.enabled = false;
        foreach (AudioListener listener in cpu.GetComponentsInChildren<AudioListener>(true))
            listener.enabled = false;
        return cpu;
    }

    void Sit(GameObject playerObject, int seat)
    {
        if (seatRegistry.TryGetSitPose(seat, out Vector3 position, out Quaternion rotation))
            playerObject.transform.SetPositionAndRotation(position, rotation);
    }
}
