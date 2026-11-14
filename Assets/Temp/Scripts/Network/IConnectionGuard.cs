/// <summary>
/// サーバー（ホスト）の接続の許可に、通信方式ごとの確認を足す窓口。PlayerSessionService が接続・切断・キックのたびに呼ぶ。
/// </summary>
public interface IConnectionGuard
{
    /// <summary>接続してきた画面が送ってきたデータを見て、入れてよいか（バージョンは PlayerSessionService が確かめ済み）</summary>
    bool Accepts(ConnectionPayload payload);

    void HandleConnected(ulong clientId);

    void HandleDisconnected(ulong clientId);

    /// <summary>部屋主がこの接続のプレイヤーをキックした（切断する直前に呼ぶ）</summary>
    void HandleKicked(ulong clientId);
}
