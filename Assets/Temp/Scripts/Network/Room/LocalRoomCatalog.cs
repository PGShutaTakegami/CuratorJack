using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Local 環境の部屋一覧。同じ PC 内の一時ファイルで部屋を共有する。
/// パスワードも部屋情報に入っているため、入室前にロビーで照合する（同じ PC 内のテスト用）。
/// ゲームのバージョンが違う部屋は表示しない。
/// </summary>
public sealed class LocalRoomCatalog : IRoomCatalog
{
    readonly IRoomDirectory directory = new LocalFileRoomDirectory();

    public Task<IReadOnlyList<RoomInfo>> FetchRoomsAsync()
    {
        IReadOnlyList<RoomInfo> rooms = directory.FetchRooms().Where(room => GameVersion.IsCompatible(room.version)).ToList();
        return Task.FromResult(rooms);
    }

    public RoomInfo CreateRoom(int capacity, string roomName, string message, string password)
    {
        return LocalRoomFactory.Create(capacity, roomName, message, password);
    }

    public bool AcceptsPassword(RoomInfo room, string password)
    {
        return !room.hasPassword || room.password == password;
    }
}
