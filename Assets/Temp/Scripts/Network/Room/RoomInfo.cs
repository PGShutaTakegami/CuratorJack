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
    /// <summary>部屋の VC を OFF にしているか（初期値は ON）</summary>
    public bool voiceChatOff;
    public int playerCount;
    public string address;
    public int port;
    public long heartbeatUtcTicks;
    /// <summary>部屋を作った時刻。リトライで同時に部屋が作られたとき、先に作られた方へまとめるのに使う</summary>
    public long createdUtcTicks;
    /// <summary>リトライで作り直した部屋なら、直前に遊んだ部屋の ID（それ以外は空）</summary>
    public string rematchOf;

    public bool IsFull => playerCount >= capacity;
}
