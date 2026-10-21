using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Multiplayer;

/// <summary>
/// OnlineNet 環境の部屋一覧。Unity Gaming Services の Lobby に公開されている部屋を取得するため、別の PC・ネットワークで作られた部屋も表示される。
/// ゲームが始まった（ロックされた）部屋は表示しない。
/// </summary>
public sealed class OnlineRoomCatalog : IRoomCatalog
{
    public const string MessageKey = "message";

    /// <summary>部屋の人数。セッションの最大人数（席の数）とは別に、ルーム設定で変えた人数を公開する</summary>
    public const string CapacityKey = "capacity";

    /// <summary>リトライでホストが作り直した部屋に付ける、直前の部屋の ID</summary>
    public const string RematchKey = "rematchOf";

    public async Task<IReadOnlyList<RoomInfo>> FetchRoomsAsync()
    {
        await OnlineServices.EnsureSignedInAsync();
        QuerySessionsResults results = await MultiplayerService.Instance.QuerySessionsAsync(new QuerySessionsOptions());

        var rooms = new List<RoomInfo>();
        foreach (ISessionInfo session in results.Sessions)
        {
            if (session.IsLocked)
                continue;

            rooms.Add(new RoomInfo
            {
                roomId = session.Id,
                roomName = session.Name,
                message = MessageOf(session),
                capacity = CapacityOf(session),
                playerCount = session.MaxPlayers - session.AvailableSlots
            });
        }

        return rooms;
    }

    /// <summary>roomId はセッション作成時に Lobby が決めるため、ここでは空のまま渡す。</summary>
    public RoomInfo CreateRoom(int capacity, string roomName, string message)
    {
        return new RoomInfo
        {
            roomName = roomName,
            message = message,
            capacity = capacity
        };
    }

    /// <summary>直前の部屋（previousRoomId）を作り直した部屋を探す。まだ無ければ null。</summary>
    public async Task<string> FindRematchRoomIdAsync(string previousRoomId)
    {
        await OnlineServices.EnsureSignedInAsync();
        QuerySessionsResults results = await MultiplayerService.Instance.QuerySessionsAsync(new QuerySessionsOptions());

        foreach (ISessionInfo session in results.Sessions)
        {
            if (!session.IsLocked && PropertyOf(session, RematchKey) == previousRoomId)
                return session.Id;
        }

        return null;
    }

    static string MessageOf(ISessionInfo session) => PropertyOf(session, MessageKey);

    static int CapacityOf(ISessionInfo session) => int.TryParse(PropertyOf(session, CapacityKey), out int capacity) ? capacity : session.MaxPlayers;

    static string PropertyOf(ISessionInfo session, string key)
    {
        return session.Properties != null && session.Properties.TryGetValue(key, out SessionProperty property)
            ? property.Value
            : string.Empty;
    }
}
