using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Local 環境の部屋一覧。同じ PC 内の一時ファイルで部屋を共有する。
/// </summary>
public sealed class LocalRoomCatalog : IRoomCatalog
{
    readonly IRoomDirectory directory = new LocalFileRoomDirectory();

    public Task<IReadOnlyList<RoomInfo>> FetchRoomsAsync()
    {
        return Task.FromResult(directory.FetchRooms());
    }

    public RoomInfo CreateRoom(int capacity, string roomName, string message)
    {
        return LocalRoomFactory.Create(capacity, roomName, message);
    }
}
