using System.Collections.Generic;

/// <summary>
/// Local の部屋一覧から、リトライで作り直された部屋を探す。
/// 同時に複数作られたときは、全員が同じ部屋へ集まるよう最も先に作られた部屋を選ぶ。
/// </summary>
public static class LocalRematchRooms
{
    /// <summary>直前の部屋（previousRoomId）を作り直した部屋のうち、最も先に作られたもの。無ければ null。</summary>
    public static RoomInfo FindEarliest(IRoomDirectory directory, string previousRoomId)
    {
        RoomInfo earliest = null;
        foreach (RoomInfo room in directory.FetchRooms())
        {
            if (room.rematchOf != previousRoomId)
                continue;

            if (earliest == null || IsEarlier(room, earliest))
                earliest = room;
        }

        return earliest;
    }

    public static bool Exists(IRoomDirectory directory, string roomId)
    {
        foreach (RoomInfo room in directory.FetchRooms())
        {
            if (room.roomId == roomId)
                return true;
        }

        return false;
    }

    static bool IsEarlier(RoomInfo room, RoomInfo other)
    {
        if (room.createdUtcTicks != other.createdUtcTicks)
            return room.createdUtcTicks < other.createdUtcTicks;

        return string.CompareOrdinal(room.roomId, other.roomId) < 0;
    }
}
