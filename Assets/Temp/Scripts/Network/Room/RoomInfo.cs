using System;

/// <summary>
/// 部屋一覧に表示する部屋 1 つ分の情報。
/// </summary>
[Serializable]
public class RoomInfo
{
    public string roomId;
    public string roomName;
    public string message;
    public int capacity;
    /// <summary>出品・落札の待ち時間（秒）。RoomWaitTime.Infinite は時間切れなし</summary>
    public int waitSeconds;
    public int playerCount;
    public string address;
    public int port;
    public long heartbeatUtcTicks;

    public bool IsFull => playerCount >= capacity;
}
