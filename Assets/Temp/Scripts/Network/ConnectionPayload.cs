using System.Text;

/// <summary>
/// 接続するときにホストへ送るデータ（ConnectionData）。ゲームのバージョンと、オンラインなら自分のプレイヤー ID（Local では空）を持つ。
/// 読めない・大きすぎるデータは、バージョンもプレイヤー ID も空として扱う（バージョンが合わないので断られる）。
/// </summary>
public sealed class ConnectionPayload
{
    const char Separator = '\n';
    const int MaxBytes = 256;

    public string Version { get; }

    public string PlayerId { get; }

    public ConnectionPayload(string version, string playerId)
    {
        Version = version ?? string.Empty;
        PlayerId = playerId ?? string.Empty;
    }

    public byte[] ToBytes() => Encoding.UTF8.GetBytes(Version + Separator + PlayerId);

    public static ConnectionPayload Parse(byte[] data)
    {
        if (data == null || data.Length == 0 || data.Length > MaxBytes)
            return new ConnectionPayload(string.Empty, string.Empty);

        string text = Encoding.UTF8.GetString(data);
        int separator = text.IndexOf(Separator);
        return separator < 0
            ? new ConnectionPayload(string.Empty, string.Empty)
            : new ConnectionPayload(text.Substring(0, separator), text.Substring(separator + 1));
    }
}
