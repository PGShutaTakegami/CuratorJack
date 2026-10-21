using Unity.Netcode;

/// <summary>
/// ゲーム開始と出品者決定をホストだけに許可し、決めた内容をホストのプレイヤーから全員へ通知する。
/// 通信方式（Local / OnlineNet）に関係なく共通で使う。
/// </summary>
public sealed class HostGameAuthority
{
    readonly NetworkManager networkManager;
    readonly GameStartGate startGate;
    readonly SellerSelector sellerSelector;
    readonly PlayerRegistry playerRegistry;
    readonly System.Random random = new System.Random();

    public HostGameAuthority(NetworkManager networkManager, GameStartGate startGate, SellerSelector sellerSelector, PlayerRegistry playerRegistry)
    {
        this.networkManager = networkManager;
        this.startGate = startGate;
        this.sellerSelector = sellerSelector;
        this.playerRegistry = playerRegistry;
    }

    /// <summary>ホストになったときに呼ぶ。スタートと出品者決定の操作を受け付けるようにする。</summary>
    public void Grant()
    {
        startGate.AllowLocalStart(() =>
        {
            if (TryGetHostPlayer(out NetworkSessionPlayer player))
                player.AnnounceGameStart(random.Next());
        });

        sellerSelector.AllowLocalSelect(() =>
        {
            if (TryGetHostPlayer(out NetworkSessionPlayer player))
                player.AnnounceSeller(SellerSelectionPolicy.Choose(playerRegistry.PlayerNumbers, random));
        });
    }

    bool TryGetHostPlayer(out NetworkSessionPlayer player)
    {
        player = null;
        NetworkObject hostPlayer = networkManager.LocalClient?.PlayerObject;
        return hostPlayer != null && hostPlayer.TryGetComponent(out player);
    }
}
