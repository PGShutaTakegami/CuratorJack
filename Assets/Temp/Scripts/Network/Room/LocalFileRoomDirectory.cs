using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 同じ PC 内のインスタンス間で部屋を共有する（Local 用）。
/// 一時フォルダに部屋ごとの JSON を置き、ホストが定期的に更新する。更新が途絶えた部屋は破棄する。
/// </summary>
public sealed class LocalFileRoomDirectory : IRoomDirectory
{
    static readonly TimeSpan StaleAfter = TimeSpan.FromSeconds(5);

    readonly string directory;

    public LocalFileRoomDirectory()
        : this(Path.Combine(Path.GetTempPath(), "CuratorJack", "LocalRooms"))
    {
    }

    public LocalFileRoomDirectory(string directory)
    {
        this.directory = directory;
        Directory.CreateDirectory(directory);
    }

    public void Publish(RoomInfo room)
    {
        room.heartbeatUtcTicks = DateTime.UtcNow.Ticks;
        string path = PathOf(room.roomId);
        string temp = path + ".tmp";
        try
        {
            // 読み込み側が書きかけのファイルを読まないよう、一時ファイルを経由して置き換える
            File.WriteAllText(temp, JsonUtility.ToJson(room));
            if (File.Exists(path))
                File.Replace(temp, path, null);
            else
                File.Move(temp, path);
        }
        catch (IOException e)
        {
            Debug.LogWarning($"[Room] 部屋情報を書き込めませんでした: {e.Message}");
        }
    }

    public void Remove(string roomId)
    {
        TryDelete(PathOf(roomId));
    }

    public IReadOnlyList<RoomInfo> FetchRooms()
    {
        var rooms = new List<RoomInfo>();
        long staleBefore = (DateTime.UtcNow - StaleAfter).Ticks;

        foreach (string path in Directory.GetFiles(directory, "*.json"))
        {
            RoomInfo room = TryRead(path);
            if (room == null)
                continue;

            if (room.heartbeatUtcTicks < staleBefore)
            {
                TryDelete(path);
                continue;
            }

            rooms.Add(room);
        }

        rooms.Sort((a, b) => string.CompareOrdinal(a.roomName, b.roomName));
        return rooms;
    }

    string PathOf(string roomId)
    {
        return Path.Combine(directory, roomId + ".json");
    }

    static RoomInfo TryRead(string path)
    {
        try
        {
            return JsonUtility.FromJson<RoomInfo>(File.ReadAllText(path));
        }
        catch (Exception)
        {
            return null;
        }
    }

    static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception)
        {
        }
    }
}
