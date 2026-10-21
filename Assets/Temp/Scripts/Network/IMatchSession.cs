using System.Threading.Tasks;

/// <summary>
/// ゲーム終了後の進め方（リトライ / タイトル）を、接続方式（SinglePlay / Local / OnlineNet）ごとに受け持つ。
/// シーンの切り替えは呼び出し側が行い、ここでは受け渡しの準備と接続の片付けだけを行う。
/// </summary>
public interface IMatchSession
{
    /// <summary>このシーンで動いている接続方式か</summary>
    bool IsActive { get; }

    /// <summary>ゲームが終わった。以降は相手が抜けてもロビーへ戻さず、リザルトに残す</summary>
    void FinishMatch();

    /// <summary>同じ顔ぶれでもう一度遊べるよう受け渡しを残してから、接続を片付ける</summary>
    Task PrepareRetryAsync();

    /// <summary>部屋の受け渡しを消してから、接続を片付ける</summary>
    Task PrepareTitleAsync();
}
