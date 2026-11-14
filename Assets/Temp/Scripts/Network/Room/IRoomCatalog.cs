using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// ロビー画面で使う、部屋一覧の取得と新しく作る部屋の情報の用意。通信方式（Local / OnlineNet）ごとに実装を差し替える。
/// </summary>
public interface IRoomCatalog
{
    Task<IReadOnlyList<RoomInfo>> FetchRoomsAsync();

    /// <summary>password が空ならパスワードなしの部屋にする。</summary>
    RoomInfo CreateRoom(int capacity, string roomName, string message, string password);

    /// <summary>入室前に確かめられる範囲で、パスワード付きの部屋に入力したパスワードが合っているか。</summary>
    bool AcceptsPassword(RoomInfo room, string password);
}
