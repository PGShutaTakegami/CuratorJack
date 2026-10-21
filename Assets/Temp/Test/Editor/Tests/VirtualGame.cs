using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// シーンを使わずに、GameManager と参加プレイヤーだけの仮想の対戦卓を用意する（ゲーム開始済みの状態）。
/// EditMode では Awake が呼ばれないため、GameManager の Awake は自前で呼ぶ。
/// </summary>
public sealed class VirtualGame : IDisposable
{
    readonly GameObject root;

    public VirtualGame(int players, int coins = 100, int passRights = 3)
    {
        root = new GameObject("VirtualGame") { hideFlags = HideFlags.HideAndDontSave };
        StartGate = root.AddComponent<GameStartGate>();
        Registry = root.AddComponent<PlayerRegistry>();
        Game = root.AddComponent<GameManager>();

        var serialized = new SerializedObject(Game);
        serialized.FindProperty("startGate").objectReferenceValue = StartGate;
        serialized.FindProperty("registry").objectReferenceValue = Registry;
        serialized.FindProperty("initialCoins").intValue = coins;
        serialized.FindProperty("initialPassRights").intValue = passRights;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        for (int player = 1; player <= players; player++)
        {
            var body = new GameObject($"Player{player}") { hideFlags = HideFlags.HideAndDontSave };
            body.transform.SetParent(root.transform);
            Registry.Register(player, body.transform);
        }
        Registry.SetLocalPlayer(1);

        typeof(GameManager).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(Game, null);
        StartGate.NotifyStarted();
    }

    public GameManager Game { get; }

    public PlayerRegistry Registry { get; }

    public GameStartGate StartGate { get; }

    public PlayerState Player(int player) => Game.TryGet(player, out PlayerState state) ? state : null;

    public int[] Alive => Game.AlivePlayers.Select(player => player.PlayerNumber).ToArray();

    public void Dispose() => UnityEngine.Object.DestroyImmediate(root);
}
