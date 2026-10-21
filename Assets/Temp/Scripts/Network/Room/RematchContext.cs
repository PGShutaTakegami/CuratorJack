/// <summary>
/// リザルトでリトライしたとき、直前に遊んだ部屋の ID を GameScene の読み直し後へ受け渡す。
/// クライアントはこの ID を目印に、ホストが作り直した部屋を探して入る。
/// </summary>
public static class RematchContext
{
    static string previousRoomId;

    public static void Set(string roomId)
    {
        previousRoomId = roomId;
    }

    /// <summary>受け渡された ID を取り出す（取り出したら消える）。</summary>
    public static bool TryTake(out string roomId)
    {
        roomId = previousRoomId;
        previousRoomId = null;
        return !string.IsNullOrEmpty(roomId);
    }

    public static void Clear()
    {
        previousRoomId = null;
    }
}
