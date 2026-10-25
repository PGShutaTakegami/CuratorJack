using UnityEngine;

/// <summary>
/// ゲームの途中で抜けられるか（ポーズの Quit とリタイア後の選択で使う）。
/// SinglePlay は脱落したら抜けられ、最初からやり直せる。マルチプレイは脱落したクライアントだけ抜けられ、
/// ホストは抜けると他の人の試合も終わるため決着まで抜けられない。最初からはマルチプレイでは選べない。
/// </summary>
public class MatchLeaveRule : MonoBehaviour
{
    [SerializeField] private SinglePlaySessionBootstrap singlePlay;

    [SerializeField] private LocalRoomOwner roomOwner;

    [SerializeField] private GameStartGate startGate;

    [SerializeField] private GameManager game;

    [SerializeField] private PlayerRegistry registry;

    /// <summary>脱落した後に、試合から抜けてよいか</summary>
    public bool CanLeaveAfterDropOut => singlePlay.IsActive || !roomOwner.IsOwner;

    /// <summary>リタイア後に、最初からやり直せるか</summary>
    public bool CanRestart => singlePlay.IsActive;

    /// <summary>Quit を押せるか（準備中か、自分が脱落していて抜けてよいとき）</summary>
    public bool CanQuit => !startGate.HasStarted || (!game.IsAlive(registry.LocalPlayerNumber) && CanLeaveAfterDropOut);
}
