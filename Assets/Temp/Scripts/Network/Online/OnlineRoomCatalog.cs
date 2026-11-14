using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.Multiplayer;

/// <summary>
/// OnlineNet 環境の部屋一覧。Unity Gaming Services の Lobby に公開されている部屋を取得するため、別の PC・ネットワークで作られた部屋も表示される。
/// ゲームが始まった（ロックされた）部屋と、ゲームのバージョンが違う部屋は表示しない。
/// </summary>
public sealed class OnlineRoomCatalog : IRoomCatalog
{
    public const string MessageKey = "message";

    /// <summary>部屋の人数。セッションの最大人数（席の数）とは別に、ルーム設定で変えた人数を公開する</summary>
    public const string CapacityKey = "capacity";

    /// <summary>リトライでホストが作り直した部屋に付ける、直前の部屋の ID</summary>
    public const string RematchKey = "rematchOf";

    /// <summary>部屋を作った画面のゲームのバージョン。違うバージョンの部屋は一覧にもリトライの合流先にも出さない</summary>
    public const string VersionKey = "version";

    public async Task<IReadOnlyList<RoomInfo>> FetchRoomsAsync()
    {
        await OnlineServices.EnsureSignedInAsync();
        QuerySessionsResults results = await MultiplayerService.Instance.QuerySessionsAsync(new QuerySessionsOptions());

        var rooms = new List<RoomInfo>();
        foreach (ISessionInfo session in results.Sessions)
        {
            if (session.IsLocked || !IsSameVersion(session))
                continue;

            rooms.Add(new RoomInfo
            {
                roomId = session.Id,
                roomName = RichTextGuard.Strip(session.Name),
                message = RichTextGuard.Strip(MessageOf(session)),
                capacity = CapacityOf(session),
                playerCount = session.MaxPlayers - session.AvailableSlots,
                hasPassword = session.HasPassword
            });
        }

        return rooms;
    }

    /// <summary>roomId はセッション作成時に Lobby が決めるため、ここでは空のまま渡す。</summary>
    public RoomInfo CreateRoom(int capacity, string roomName, string message, string password)
    {
        return new RoomInfo
        {
            roomName = RichTextGuard.Strip(roomName),
            message = RichTextGuard.Strip(message),
            capacity = capacity,
            hasPassword = !string.IsNullOrEmpty(password),
            password = password ?? string.Empty
        };
    }

    /// <summary>パスワードは Lobby からは読めないため、入室時に Lobby が照合する。</summary>
    public bool AcceptsPassword(RoomInfo room, string password) => true;

    /// <summary>
    /// 直前の部屋（previousRoomId）を作り直した部屋を探す。まだ無ければ null。
    /// 同時に複数作られたときは、全員が同じ部屋へ集まるよう最も先に作られた部屋を返す。
    /// previousMemberIds が空でなければ、ホストが直前の部屋のメンバーである部屋だけを選ぶ（メンバー以外が目印を付けて作った偽の部屋に入らないため）。
    /// </summary>
    public async Task<string> FindRematchRoomIdAsync(string previousRoomId, IReadOnlyCollection<string> previousMemberIds)
    {
        await OnlineServices.EnsureSignedInAsync();
        QuerySessionsResults results = await MultiplayerService.Instance.QuerySessionsAsync(new QuerySessionsOptions());

        bool checksHost = previousMemberIds != null && previousMemberIds.Count > 0;
        ISessionInfo earliest = null;
        foreach (ISessionInfo session in results.Sessions)
        {
            if (session.IsLocked || !IsSameVersion(session) || PropertyOf(session, RematchKey) != previousRoomId)
                continue;
            if (checksHost && !previousMemberIds.Contains(session.HostId))
                continue;

            if (earliest == null || IsEarlier(session, earliest))
                earliest = session;
        }

        return earliest?.Id;
    }

    static bool IsEarlier(ISessionInfo session, ISessionInfo other)
    {
        if (session.Created != other.Created)
            return session.Created < other.Created;

        return string.CompareOrdinal(session.Id, other.Id) < 0;
    }

    static string MessageOf(ISessionInfo session) => PropertyOf(session, MessageKey);

    static bool IsSameVersion(ISessionInfo session) => GameVersion.IsCompatible(PropertyOf(session, VersionKey));

    static int CapacityOf(ISessionInfo session) => int.TryParse(PropertyOf(session, CapacityKey), out int capacity) ? capacity : session.MaxPlayers;

    static string PropertyOf(ISessionInfo session, string key)
    {
        return session.Properties != null && session.Properties.TryGetValue(key, out SessionProperty property)
            ? property.Value
            : string.Empty;
    }
}
