#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

/// <summary>
/// テスト用の道具が使う、GameScene のコンポーネントの参照。シーンが変わったら Find し直す。
/// </summary>
public sealed class TestSceneRefs
{
    TestSceneRefs()
    {
        Flow = Object.FindFirstObjectByType<TurnFlowController>();
        Game = Object.FindFirstObjectByType<GameManager>();
        Registry = Object.FindFirstObjectByType<PlayerRegistry>();
        StartGate = Object.FindFirstObjectByType<GameStartGate>();
        LocalAgent = Object.FindFirstObjectByType<LocalPlayerAgent>();
        SinglePlay = Object.FindFirstObjectByType<SinglePlaySessionBootstrap>();
        Names = Object.FindFirstObjectByType<PlayerNameDirectory>();
        GoodsDeck = Object.FindFirstObjectByType<GoodsCardDeck>();
        EffectDeck = Object.FindFirstObjectByType<EffectCardDeck>();
        SpecialDeck = Object.FindFirstObjectByType<SpecialCardDeck>();
    }

    public static TestSceneRefs Find() => new TestSceneRefs();

    public TurnFlowController Flow { get; }

    public GameManager Game { get; }

    public PlayerRegistry Registry { get; }

    public GameStartGate StartGate { get; }

    public LocalPlayerAgent LocalAgent { get; }

    public SinglePlaySessionBootstrap SinglePlay { get; }

    public PlayerNameDirectory Names { get; }

    public GoodsCardDeck GoodsDeck { get; }

    public EffectCardDeck EffectDeck { get; }

    public SpecialCardDeck SpecialDeck { get; }

    public bool IsGameScene => Flow != null && Game != null && Registry != null;

    /// <summary>ネットワークを使わない SinglePlay か（状態を直接書き換えても全員の画面がずれない）</summary>
    public bool IsSinglePlay => IsGameScene && SinglePlay != null && SinglePlay.IsActive;

    public bool HasStarted => StartGate != null && StartGate.HasStarted;

    public int LocalPlayer => Registry != null ? Registry.LocalPlayerNumber : PlayerRegistry.NoPlayer;

    public string NameOf(int player) => Names != null ? Names.NameOf(player) : PlayerNameDirectory.FallbackOf(player);
}
#endif
