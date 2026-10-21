using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// ロビー画面で使う、部屋一覧の取得と新しく作る部屋の情報の用意。通信方式（Local / OnlineNet）ごとに実装を差し替える。
/// </summary>
public interface IRoomCatalog
{
    Task<IReadOnlyList<RoomInfo>> FetchRoomsAsync();

    RoomInfo CreateRoom(int capacity, string roomName, string message);
}
