/// <summary>
/// 部屋のパスワードを Lobby のセッションのパスワードに変換する。Lobby は 8〜64 文字しか受け付けないため、固定の文字列を前に付ける。
/// </summary>
public static class OnlineRoomPassword
{
    const string Prefix = "CuratorJack-room:";

    public static string ToSessionPassword(string password) => Prefix + password;
}
