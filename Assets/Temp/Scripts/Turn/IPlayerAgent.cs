/// <summary>
/// プレイヤー 1 人分の操作役（自分の画面操作・コンピューター・将来のネットワーク越しの相手）。
/// 進行役は相手が誰かを区別せず、手続き（ListingSession など）を渡して操作を待つ。
/// </summary>
public interface IPlayerAgent
{
    int PlayerNumber { get; }

    /// <summary>出品者として、出品する商品を決める</summary>
    void BeginListing(ListingSession session);

    /// <summary>オークションが始まった。参加者は入札するか降りるかを選ぶ（参加していないプレイヤーにも通知する）</summary>
    void BeginAuction(AuctionModel auction);

    /// <summary>今の手続きが終わった。操作をやめる</summary>
    void EndPhase();
}
