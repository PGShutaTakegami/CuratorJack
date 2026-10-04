/// <summary>
/// ロビーで決めた部屋（作成 or 入室）を GameScene へ受け渡す。
/// </summary>
public static class RoomConnectionContext
{
    public enum Role
    {
        None,
        Host,
        Client
    }

    public static Role CurrentRole { get; private set; }

    public static RoomInfo Room { get; private set; }

    public static bool HasRoom => CurrentRole != Role.None && Room != null;

    public static void SetHost(RoomInfo room)
    {
        CurrentRole = Role.Host;
        Room = room;
    }

    public static void SetClient(RoomInfo room)
    {
        CurrentRole = Role.Client;
        Room = room;
    }

    public static void Clear()
    {
        CurrentRole = Role.None;
        Room = null;
    }
}
