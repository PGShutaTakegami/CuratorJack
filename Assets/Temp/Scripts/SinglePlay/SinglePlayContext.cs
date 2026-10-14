/// <summary>
/// SinglePlay で選んだ人数（自分を含む合計）を GameScene へ受け渡す。
/// </summary>
public static class SinglePlayContext
{
    public static bool IsActive { get; private set; }

    public static int TotalPlayers { get; private set; }

    public static void Set(int totalPlayers)
    {
        TotalPlayers = totalPlayers;
        IsActive = true;
    }

    public static void Clear()
    {
        TotalPlayers = 0;
        IsActive = false;
    }
}
