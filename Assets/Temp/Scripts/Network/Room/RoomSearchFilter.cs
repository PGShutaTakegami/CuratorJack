using System;
using System.Collections.Generic;

public enum RoomPasswordCondition
{
    Any,
    With,
    Without
}

/// <summary>
/// 部屋一覧の絞り込み条件（ルーム名の部分一致・人数・パスワードの有無）。取得済みの一覧を絞り込むだけで通信はしない。
/// </summary>
public sealed class RoomSearchFilter
{
    public static readonly RoomSearchFilter None = new RoomSearchFilter(string.Empty, 0, RoomPasswordCondition.Any);

    public string NameKeyword { get; }

    /// <summary>0 なら人数を問わない</summary>
    public int Capacity { get; }

    public RoomPasswordCondition Password { get; }

    public RoomSearchFilter(string nameKeyword, int capacity, RoomPasswordCondition password)
    {
        NameKeyword = (nameKeyword ?? string.Empty).Trim();
        Capacity = Math.Max(0, capacity);
        Password = password;
    }

    public RoomSearchFilter WithCapacity(int capacity) => new RoomSearchFilter(NameKeyword, capacity, Password);

    public bool Matches(RoomInfo room)
    {
        if (Capacity > 0 && room.capacity != Capacity)
            return false;

        if (Password == RoomPasswordCondition.With && !room.hasPassword)
            return false;

        if (Password == RoomPasswordCondition.Without && room.hasPassword)
            return false;

        return NameKeyword.Length == 0
            || (room.roomName ?? string.Empty).IndexOf(NameKeyword, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public List<RoomInfo> Apply(IEnumerable<RoomInfo> rooms)
    {
        var matched = new List<RoomInfo>();
        foreach (RoomInfo room in rooms)
        {
            if (Matches(room))
                matched.Add(room);
        }

        return matched;
    }
}
