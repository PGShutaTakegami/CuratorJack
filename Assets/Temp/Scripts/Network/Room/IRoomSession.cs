using System.Threading.Tasks;

/// <summary>
/// マルチプレイの部屋から、ゲーム開始前に抜けるための片付け（Local / OnlineNet ごと）。
/// シーンの切り替えは呼び出し側が行う。
/// </summary>
public interface IRoomSession
{
    /// <summary>このシーンで動いている接続方式か</summary>
    bool IsActive { get; }

    /// <summary>部屋の受け渡しを消し、ロビーを MultiPlay 画面で開くよう指定してから、部屋を抜けて接続を片付ける</summary>
    Task PrepareLeaveRoomAsync();
}
