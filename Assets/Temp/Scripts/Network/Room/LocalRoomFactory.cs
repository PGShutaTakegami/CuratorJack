using System;
using System.Net;
using System.Net.Sockets;

/// <summary>
/// 同じ PC 内（127.0.0.1）で使う部屋情報を作る。ポートは空いているものを割り当てる。
/// </summary>
public static class LocalRoomFactory
{
    const string LoopbackAddress = "127.0.0.1";

    public static RoomInfo Create(int capacity, string roomName, string message)
    {
        return new RoomInfo
        {
            roomId = Guid.NewGuid().ToString("N"),
            roomName = roomName,
            message = message,
            capacity = capacity,
            playerCount = 0,
            address = LoopbackAddress,
            port = FindFreePort()
        };
    }

    static int FindFreePort()
    {
        using (var probe = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0)))
            return ((IPEndPoint)probe.Client.LocalEndPoint).Port;
    }
}
