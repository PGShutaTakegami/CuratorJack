using System.Collections.Generic;

/// <summary>
/// 部屋の公開・取得を行う窓口。通信方式（Local / LocalNet / OnlineNet）ごとに実装を差し替える。
/// </summary>
public interface IRoomDirectory
{
    /// <summary>部屋を公開する（同じ roomId なら上書き）。定期的に呼んで生存を通知する</summary>
    void Publish(RoomInfo room);

    void Remove(string roomId);

    IReadOnlyList<RoomInfo> FetchRooms();
}
