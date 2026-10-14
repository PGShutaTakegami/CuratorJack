using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SinglePlay から来たとき、ネットワークを使わずにプレイヤーを配置する。
/// シーンに置かれたプレイヤーを自分（Player1）とし、残りの人数分のコンピューターを席に座らせる。
/// SinglePlay を経由しない場合は何もしない。
/// </summary>
public class SinglePlaySessionBootstrap : MonoBehaviour
{
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

    public IReadOnlyList<SinglePlayPlayer> Players => players;

    void Awake()
    {
        if (!SinglePlayContext.IsActive)
            return;

        int totalPlayers = Mathf.Min(SinglePlayContext.TotalPlayers, seatRegistry.SeatCount);
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

    void PlacePlayers(int totalPlayers)
    {
        var policy = new SeatAssignmentPolicy(seatRegistry.SeatCount, seatRegistry.FirstSeat, new System.Random());
        var seatByPlayer = new Dictionary<int, int>();

        for (int playerNumber = 1; playerNumber <= totalPlayers; playerNumber++)
        {
            int seat = policy.ChooseSeat(playerNumber, seatByPlayer);
            if (seat == SeatAssignmentPolicy.NoSeat)
                break;

            seatByPlayer[playerNumber] = seat;
            bool isCpu = playerNumber != 1;
            GameObject playerObject = isCpu ? CreateCpu(playerNumber) : localPlayer;
            Sit(playerObject, seat);

            var player = playerObject.GetComponent<SinglePlayPlayer>();
            if (player == null)
                player = playerObject.AddComponent<SinglePlayPlayer>();
            player.Initialize(playerNumber, isCpu);
            players.Add(player);
            playerRegistry.Register(playerNumber, playerObject.transform);
            if (!isCpu)
                playerRegistry.SetLocalPlayer(playerNumber);
        }
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
                cpu.Initialize(player.PlayerNumber, game);
                agents.Add(cpu);
            }
            else
            {
                localAgent.Initialize(player.PlayerNumber);
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
