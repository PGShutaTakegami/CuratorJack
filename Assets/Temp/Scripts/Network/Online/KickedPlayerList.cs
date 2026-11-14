using System.Collections.Generic;
using System.Text;

/// <summary>
/// 部屋主がキックしたプレイヤーの ID（Unity Authentication の PlayerId）の一覧。同じ部屋に入り直させないために使う。
/// ホストが代わっても引き継げるよう、バイト列にして受け渡せる。
/// </summary>
public sealed class KickedPlayerList
{
    const char Separator = '\n';

    readonly HashSet<string> ids = new HashSet<string>();

    public bool Contains(string playerId) => !string.IsNullOrEmpty(playerId) && ids.Contains(playerId);

    public void Add(string playerId)
    {
        if (!string.IsNullOrEmpty(playerId))
            ids.Add(playerId);
    }

    public byte[] Serialize() => Encoding.UTF8.GetBytes(string.Join(Separator.ToString(), ids));

    /// <summary>Serialize したものから一覧を作り直す。空や読めないものなら空の一覧になる</summary>
    public void Load(byte[] data)
    {
        ids.Clear();
        if (data == null || data.Length == 0)
            return;

        foreach (string id in Encoding.UTF8.GetString(data).Split(Separator))
            Add(id.Trim('\0', ' ', '\r'));
    }
}
