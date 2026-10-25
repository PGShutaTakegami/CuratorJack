/// <summary>
/// リザルトでリトライしたとき、直前に遊んだ部屋の情報（RematchTicket）を GameScene の読み直し後へ受け渡す。
/// 最初にリトライした人がこの部屋の ID を目印に部屋を作り直し、後から来た人はその部屋を探して入る。
/// </summary>
public static class RematchContext
{
    static RematchTicket ticket;

    public static void Set(RematchTicket rematchTicket)
    {
        ticket = rematchTicket;
    }

    /// <summary>受け渡された情報を取り出す（取り出したら消える）。</summary>
    public static bool TryTake(out RematchTicket rematchTicket)
    {
        rematchTicket = ticket;
        ticket = null;
        return rematchTicket != null && !string.IsNullOrEmpty(rematchTicket.PreviousRoomId);
    }

    public static void Clear()
    {
        ticket = null;
    }
}
