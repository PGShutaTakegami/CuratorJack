#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 全員 CPU の SinglePlay を高速で何試合も回し、結果を集計する（実際のゲームの処理で進めるので、ルールは二重に書かない）。
/// 1 試合ごとに GameScene を読み直し、自分も CPU にしてスタートする。決着がつかないまま realTimeout 秒を超えた試合は打ち切る。
/// 終わったら要約をログに出し、要約と CSV を Logs/TestReports（ビルドでは persistentDataPath）に保存する。
/// </summary>
public class MatchSimulationRunner : MonoBehaviour
{
    const string GameSceneName = "GameScene";

    int matches;
    int players;
    float speed;
    float realTimeout;
    bool cancelRequested;
    readonly MatchStatistics statistics = new MatchStatistics();

    public static MatchSimulationRunner Current { get; private set; }

    public static bool IsRunning => Current != null;

    public int Completed => statistics.Records.Count;

    public int Total => matches;

    public static void Begin(int matches, int players, float speed = 16f, float realTimeout = 300f)
    {
        if (IsRunning)
            return;

        var runner = new GameObject("[MatchSimulationRunner]").AddComponent<MatchSimulationRunner>();
        DontDestroyOnLoad(runner.gameObject);
        runner.matches = Mathf.Max(1, matches);
        runner.players = Mathf.Max(2, players);
        runner.speed = speed;
        runner.realTimeout = realTimeout;
        Current = runner;
        runner.StartCoroutine(runner.Run());
    }

    /// <summary>今の試合で打ち切り、そこまでの結果を集計する</summary>
    public static void Cancel()
    {
        if (IsRunning)
            Current.cancelRequested = true;
    }

    IEnumerator Run()
    {
        Debug.Log($"[Test] シミュレーション開始：{players} 人 × {matches} 試合（x{speed}）");
        for (int i = 1; i <= matches && !cancelRequested; i++)
        {
            yield return LoadGameScene();
            yield return PlayMatch(i);
        }

        Report();
        TestAutoPilot.TurnOff(TestSceneRefs.Find());
        TestTimeScale.ResetToNormal();
        Current = null;
        Destroy(gameObject);
    }

    IEnumerator LoadGameScene()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
            Destroy(NetworkManager.Singleton.gameObject);
        }
        SinglePlayContext.Set(players);
        yield return SceneManager.LoadSceneAsync(GameSceneName);
        yield return null;
    }

    IEnumerator PlayMatch(int index)
    {
        TestSceneRefs refs = TestSceneRefs.Find();
        if (!refs.IsSinglePlay)
        {
            Debug.LogError("[Test] SinglePlay の GameScene を用意できなかったため、シミュレーションを中止します");
            cancelRequested = true;
            yield break;
        }

        var record = new MatchRecord(index, players);
        bool finished = false;
        AuctionModel auction = null;
        Action<int> onRound = round => record.Rounds = round;
        Action<int> onTurn = _ => record.Turns++;
        Action<AuctionModel> onAuctionStarted = started => auction = started;
        Action onAuctionFinished = () =>
        {
            record.Auctions++;
            if (auction != null && auction.Winner != PlayerRegistry.NoPlayer)
            {
                record.SoldAuctions++;
                record.TotalPrice += auction.Price;
            }
        };
        Action<PlayerState> onDied = player => record.DeathOrder.Add(player.PlayerNumber);
        Action onFinished = () => finished = true;

        refs.Flow.RoundStarted += onRound;
        refs.Flow.TurnStarted += onTurn;
        refs.Flow.AuctionStarted += onAuctionStarted;
        refs.Flow.AuctionFinished += onAuctionFinished;
        refs.Flow.GameFinished += onFinished;
        refs.Game.PlayerDied += onDied;

        TestAutoPilot.TurnOff(refs);
        TestAutoPilot.TurnOn(refs);
        TestTimeScale.Set(speed);
        float realStart = Time.realtimeSinceStartup;
        float gameStart = Time.time;
        refs.StartGate.RequestStart();
        yield return new WaitUntil(() => finished || cancelRequested || Time.realtimeSinceStartup - realStart > realTimeout);

        record.TimedOut = !finished;
        record.RealSeconds = Time.realtimeSinceStartup - realStart;
        record.GameSeconds = Time.time - gameStart;
        record.Winner = finished ? refs.Game.AlivePlayers.Select(player => player.PlayerNumber).DefaultIfEmpty(PlayerRegistry.NoPlayer).First() : PlayerRegistry.NoPlayer;
        foreach (PlayerState player in refs.Game.Players)
        {
            record.FinalCoins[player.PlayerNumber] = player.Coins;
            record.Collections[player.PlayerNumber] = player.Collection.Count;
        }

        refs.Flow.RoundStarted -= onRound;
        refs.Flow.TurnStarted -= onTurn;
        refs.Flow.AuctionStarted -= onAuctionStarted;
        refs.Flow.AuctionFinished -= onAuctionFinished;
        refs.Flow.GameFinished -= onFinished;
        refs.Game.PlayerDied -= onDied;

        if (cancelRequested && !finished)
            yield break;

        statistics.Add(record);
        string result = record.TimedOut ? "打ち切り" : $"勝者 Player{record.Winner}";
        Debug.Log($"[Test] 試合 {index}/{matches}：{result}　ラウンド {record.Rounds}　ターン {record.Turns}　実時間 {record.RealSeconds:0.0} 秒");
    }

    void Report()
    {
        string summary = statistics.Summary();
        Debug.Log(summary);
        if (statistics.Records.Count == 0)
            return;

        string folder = ReportFolder();
        Directory.CreateDirectory(folder);
        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        File.WriteAllText(Path.Combine(folder, $"simulation_{stamp}.csv"), statistics.ToCsv());
        File.WriteAllText(Path.Combine(folder, $"simulation_{stamp}.txt"), summary);
        Debug.Log($"[Test] 結果を保存しました：{folder}");
    }

    static string ReportFolder()
    {
#if UNITY_EDITOR
        return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "TestReports"));
#else
        return Path.Combine(Application.persistentDataPath, "TestReports");
#endif
    }
}
#endif
