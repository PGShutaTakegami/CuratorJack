#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// テストプレイ用のコンソール（エディタと開発ビルドだけで、起動時に自動で常駐する）。
/// F1：パネルの表示切り替え　F2 / F3：遅く / 速く　F4：等倍　F5：オートプレイ（自分を CPU に）　F6：待ち時間切れ　F7：スタート
/// パネルでは、プレイヤーの状態の一覧、起きたことにする書き換え（SinglePlay のみ）、全員 CPU の一括シミュレーションを扱う。
/// </summary>
public class TestPlayConsole : MonoBehaviour
{
    const float ReferenceHeight = 820f;
    static readonly PlayerStatus[] Statuses = (PlayerStatus[])System.Enum.GetValues(typeof(PlayerStatus));

    bool isVisible;
    TestSceneRefs refs;
    int selectedPlayer = 1;
    int simulationMatches = 10;
    int simulationPlayers = 4;
    Vector2 cardScroll;
    Rect windowRect = new Rect(16f, 60f, 600f, 700f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        var console = new GameObject("[TestPlayConsole]");
        DontDestroyOnLoad(console);
        console.AddComponent<TestPlayConsole>();
    }

    void OnEnable() => SceneManager.sceneLoaded += HandleSceneLoaded;

    void OnDisable() => SceneManager.sceneLoaded -= HandleSceneLoaded;

    void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => refs = null;

    TestSceneRefs Refs => refs ??= TestSceneRefs.Find();

    void Update()
    {
        TestTimeScale.Keep();
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (keyboard.f1Key.wasPressedThisFrame)
            isVisible = !isVisible;
        if (keyboard.f2Key.wasPressedThisFrame)
            TestTimeScale.Slower();
        if (keyboard.f3Key.wasPressedThisFrame)
            TestTimeScale.Faster();
        if (keyboard.f4Key.wasPressedThisFrame)
            TestTimeScale.ResetToNormal();
        if (keyboard.f5Key.wasPressedThisFrame && Refs.IsGameScene)
            TestAutoPilot.Toggle(Refs);
        if (keyboard.f6Key.wasPressedThisFrame)
            TestCheats.TimeOutNow(Refs);
        if (keyboard.f7Key.wasPressedThisFrame && Refs.StartGate != null)
            Refs.StartGate.RequestStart();
    }

    void OnGUI()
    {
        float scale = Screen.height / ReferenceHeight;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

        if (!TestTimeScale.IsNormal || TestAutoPilot.IsOn || MatchSimulationRunner.IsRunning)
            GUI.Label(new Rect(16f, 36f, 600f, 24f), Badge());
        if (isVisible)
            windowRect = GUI.Window(GetInstanceID(), windowRect, DrawWindow, "テストプレイ（F1 で閉じる）");
    }

    string Badge()
    {
        var parts = new List<string> { $"x{TestTimeScale.Current}" };
        if (TestAutoPilot.IsOn)
            parts.Add("オートプレイ");
        if (MatchSimulationRunner.IsRunning)
            parts.Add($"シミュレーション {MatchSimulationRunner.Current.Completed}/{MatchSimulationRunner.Current.Total}");
        return string.Join("　", parts);
    }

    void DrawWindow(int id)
    {
        DrawSpeed();
        DrawGame();
        DrawSimulation();
        DrawPlayers();
        DrawCheats();
        GUI.DragWindow(new Rect(0f, 0f, 10000f, 24f));
    }

    void DrawSpeed()
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label($"速度 x{TestTimeScale.Current}", GUILayout.Width(140f));
        if (GUILayout.Button("遅く (F2)"))
            TestTimeScale.Slower();
        if (GUILayout.Button("速く (F3)"))
            TestTimeScale.Faster();
        if (GUILayout.Button("等倍 (F4)"))
            TestTimeScale.ResetToNormal();
        GUILayout.EndHorizontal();
    }

    void DrawGame()
    {
        if (!Refs.IsGameScene)
        {
            GUILayout.Label("GameScene ではないため、ゲームの操作は使えません");
            return;
        }

        GUILayout.BeginHorizontal();
        if (GUILayout.Button(TestAutoPilot.IsOn ? "オートプレイ OFF (F5)" : "オートプレイ ON (F5)"))
            TestAutoPilot.Toggle(Refs);
        GUI.enabled = Refs.StartGate != null && Refs.StartGate.CanLocalStart && !Refs.HasStarted;
        if (GUILayout.Button("スタート (F7)"))
            Refs.StartGate.RequestStart();
        GUI.enabled = TestCheats.CanUse(Refs);
        if (GUILayout.Button("待ち時間切れ (F6)"))
            TestCheats.TimeOutNow(Refs);
        GUI.enabled = true;
        GUILayout.EndHorizontal();
    }

    void DrawSimulation()
    {
        GUILayout.Space(6f);
        GUILayout.Label("一括シミュレーション（全員 CPU・x16 で GameScene を読み直して回す）");
        GUILayout.BeginHorizontal();
        simulationMatches = Stepper("試合", simulationMatches, 1, 100, 10);
        simulationPlayers = Stepper("人数", simulationPlayers, 2, 4, 1);
        if (MatchSimulationRunner.IsRunning)
        {
            if (GUILayout.Button("中止"))
                MatchSimulationRunner.Cancel();
        }
        else if (GUILayout.Button("開始"))
        {
            MatchSimulationRunner.Begin(simulationMatches, simulationPlayers);
        }
        GUILayout.EndHorizontal();
    }

    static int Stepper(string label, int value, int min, int max, int step)
    {
        GUILayout.Label($"{label} {value}", GUILayout.Width(80f));
        if (GUILayout.Button("-", GUILayout.Width(28f)))
            value -= step;
        if (GUILayout.Button("+", GUILayout.Width(28f)))
            value += step;
        return Mathf.Clamp(value, min, max);
    }

    void DrawPlayers()
    {
        if (!Refs.IsGameScene || !Refs.HasStarted)
            return;

        GUILayout.Space(6f);
        PlayerState[] players = Refs.Game.Players.ToArray();
        string[] labels = players.Select(player => player.PlayerNumber == Refs.LocalPlayer ? $"P{player.PlayerNumber}（自分）" : $"P{player.PlayerNumber}").ToArray();
        int selectedIndex = System.Array.FindIndex(players, player => player.PlayerNumber == selectedPlayer);
        selectedIndex = GUILayout.Toolbar(Mathf.Max(0, selectedIndex), labels);
        if (players.Length > 0)
            selectedPlayer = players[selectedIndex].PlayerNumber;

        foreach (PlayerState player in players)
            GUILayout.Label(Describe(player));
    }

    string Describe(PlayerState player)
    {
        int goods = player.Hand.Count(card => card.IsGoods);
        string statuses = string.Join(" ", Statuses.Where(status => player.StatusOf(status) > 0).Select(status => $"{status}:{player.StatusOf(status)}"));
        string life = player.IsAlive ? string.Empty : "【脱落】";
        return $"{life}{Refs.NameOf(player.PlayerNumber)}　コイン {player.Coins}　見送り権 {player.PassRights}　手札 グッズ{goods}/効果{player.Hand.Count - goods}　落札 {player.Collection.Count}　{statuses}";
    }

    void DrawCheats()
    {
        if (!Refs.IsGameScene || !Refs.HasStarted)
            return;
        if (!TestCheats.CanUse(Refs))
        {
            GUILayout.Label("状態の書き換えは SinglePlay のときだけ使えます");
            return;
        }

        GUILayout.Space(6f);
        GUILayout.Label($"P{selectedPlayer} に起きたことにする");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("コイン +10"))
            TestCheats.AddCoins(Refs, selectedPlayer, 10);
        if (GUILayout.Button("コイン -10"))
            TestCheats.AddCoins(Refs, selectedPlayer, -10);
        if (GUILayout.Button("見送り権 +1"))
            TestCheats.AddPassRights(Refs, selectedPlayer, 1);
        if (GUILayout.Button("見送り権 -1"))
            TestCheats.AddPassRights(Refs, selectedPlayer, -1);
        if (GUILayout.Button("リタイア"))
            TestCheats.Retire(Refs, selectedPlayer);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        foreach (PlayerStatus status in Statuses)
        {
            if (GUILayout.Button(status.ToString()))
                TestCheats.AddStatus(Refs, selectedPlayer, status);
        }
        GUILayout.EndHorizontal();

        GUILayout.Label("手札に加えるカード");
        cardScroll = GUILayout.BeginScrollView(cardScroll, GUILayout.Height(220f));
        foreach (CardData card in Cards())
        {
            if (GUILayout.Button(CardLabel(card)))
                TestCheats.GiveCard(Refs, selectedPlayer, card);
        }
        GUILayout.EndScrollView();
    }

    IEnumerable<CardData> Cards()
    {
        IEnumerable<CardData> goods = Refs.GoodsDeck != null ? Refs.GoodsDeck.Goods : Enumerable.Empty<CardData>();
        IEnumerable<CardData> effects = Refs.EffectDeck != null ? Refs.EffectDeck.Effects : Enumerable.Empty<CardData>();
        return goods.Concat(effects).Where(card => card != null).Distinct();
    }

    static string CardLabel(CardData card)
    {
        return card.Effect == CardEffect.None
            ? $"グッズ：{card.Parameter.CardName}（{card.Rarity}・{card.Parameter.InitialPrice}）"
            : $"効果：{card.Parameter.CardName}（{card.Effect}）";
    }
}
#endif
