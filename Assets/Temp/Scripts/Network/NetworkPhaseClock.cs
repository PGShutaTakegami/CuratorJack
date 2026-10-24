using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// マルチプレイの待ち時間の時計。ステップごとに、対象者のうち人が操作している全員の画面から演出が終わった知らせが届いたら、
/// ホストが締め切りをネットワークの時刻で決めて全員へ配る。全員の画面はその締め切りまでの残り秒数を数えるため、表示と時間切れがそろう。
/// 切断して CPU に代わった人・CPU は待たず、readyTimeoutSeconds 待っても知らせがそろわなければ数え始める。
/// </summary>
public class NetworkPhaseClock : MonoBehaviour, IPhaseClock
{
    [Tooltip("対象者の知らせを待つ最長の秒数（過ぎたら、そろっていなくても数え始める）")]
    [SerializeField] private float readyTimeoutSeconds = 10f;

    readonly List<NetworkSessionPlayer> players = new List<NetworkSessionPlayer>();
    readonly Dictionary<int, HashSet<int>> readyPlayers = new Dictionary<int, HashSet<int>>();
    readonly Dictionary<int, double> deadlines = new Dictionary<int, double>();
    NetworkSessionPlayer localPlayer;
    Func<int, bool> isControlledByCpu;
    int step = -1;
    int[] targets = Array.Empty<int>();
    int waitSeconds;
    float stepBeganAt;
    bool hasAnnounced = true;

    /// <summary>マルチプレイのゲームが始まっていて、この時計を使うか</summary>
    public bool IsActive => localPlayer != null && localPlayer.IsSpawned;

    bool IsServer => IsActive && localPlayer.IsServer;

    double Now => localPlayer.NetworkManager.ServerTime.Time;

    /// <summary>ゲームが始まったら、自分のプレイヤーと全プレイヤー、切断して CPU が代わりに操作しているかの判定を渡す</summary>
    public void Connect(NetworkSessionPlayer local, IEnumerable<NetworkSessionPlayer> sessionPlayers, Func<int, bool> controlledByCpu)
    {
        Disconnect();
        localPlayer = local;
        isControlledByCpu = controlledByCpu;
        foreach (NetworkSessionPlayer player in sessionPlayers)
        {
            if (player == null)
                continue;
            players.Add(player);
            player.PhaseReadyReported += HandleReadyReported;
            player.PhaseCountdownAnnounced += HandleCountdownAnnounced;
        }
    }

    void OnDestroy() => Disconnect();

    void Disconnect()
    {
        foreach (NetworkSessionPlayer player in players)
        {
            if (player == null)
                continue;
            player.PhaseReadyReported -= HandleReadyReported;
            player.PhaseCountdownAnnounced -= HandleCountdownAnnounced;
        }
        players.Clear();
        localPlayer = null;
    }

    public void BeginStep(int step, IReadOnlyCollection<int> targets, int waitSeconds)
    {
        this.step = step;
        this.targets = targets.ToArray();
        this.waitSeconds = waitSeconds;
        stepBeganAt = Time.unscaledTime;
        hasAnnounced = false;
        Prune(readyPlayers.Keys.ToArray(), key => readyPlayers.Remove(key));
        Prune(deadlines.Keys.ToArray(), key => deadlines.Remove(key));
    }

    void Prune(int[] steps, Action<int> remove)
    {
        foreach (int key in steps.Where(key => key < step))
            remove(key);
    }

    public void ReportReady(int step)
    {
        if (IsActive)
            localPlayer.ReportPhaseReady(step);
    }

    public bool TryGetRemaining(int step, out float seconds)
    {
        seconds = 0f;
        if (!IsActive || step != this.step || !deadlines.TryGetValue(step, out double deadline))
            return false;

        seconds = (float)Math.Max(0d, deadline - Now);
        return true;
    }

    public void EndStep()
    {
        step = -1;
        hasAnnounced = true;
    }

    void Update()
    {
        if (!IsServer || hasAnnounced || step < 0)
            return;

        bool allReady = targets.All(player => !NeedsReport(player) || IsReported(player));
        if (!allReady && Time.unscaledTime - stepBeganAt < readyTimeoutSeconds)
            return;

        hasAnnounced = true;
        localPlayer.AnnouncePhaseCountdown(step, Now + waitSeconds);
    }

    /// <summary>人が操作していて、今もつながっている対象者だけ知らせを待つ</summary>
    bool NeedsReport(int playerNumber)
    {
        NetworkSessionPlayer player = players.Find(p => p != null && p.PlayerNumber == playerNumber);
        if (player == null || player.IsCpu || isControlledByCpu(playerNumber))
            return false;

        return localPlayer.NetworkManager.ConnectedClientsIds.Contains(player.OwnerClientId);
    }

    bool IsReported(int playerNumber) => readyPlayers.TryGetValue(step, out HashSet<int> ready) && ready.Contains(playerNumber);

    void HandleReadyReported(int playerNumber, int reportedStep)
    {
        if (reportedStep < step)
            return;

        if (!readyPlayers.TryGetValue(reportedStep, out HashSet<int> ready))
            readyPlayers[reportedStep] = ready = new HashSet<int>();
        ready.Add(playerNumber);
    }

    /// <summary>この画面でまだ始まっていないステップの締め切りも、始まったときに使えるよう取っておく</summary>
    void HandleCountdownAnnounced(int announcedStep, double deadline)
    {
        if (announcedStep >= step)
            deadlines[announcedStep] = deadline;
    }
}
