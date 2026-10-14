using UnityEngine;

/// <summary>
/// ネットワークのプレイヤーを、シーンの PlayerRegistry へ登録する（自分のプレイヤーなら自分の番号も登録する）。
/// ホストから届いた出品者の通知を、シーンの SellerSelector へ伝える。
/// </summary>
[RequireComponent(typeof(NetworkSessionPlayer))]
public class PlayerTurnReporter : MonoBehaviour
{
    NetworkSessionPlayer player;
    PlayerRegistry registry;
    SellerSelector selector;
    int registeredNumber = NetworkSessionPlayer.UnassignedNumber;

    void Awake()
    {
        player = GetComponent<NetworkSessionPlayer>();
        registry = FindFirstObjectByType<PlayerRegistry>();
        selector = FindFirstObjectByType<SellerSelector>();
        player.PlayerNumberChanged += HandlePlayerNumberChanged;
        player.SellerDecided += HandleSellerDecided;
    }

    void OnDestroy()
    {
        player.PlayerNumberChanged -= HandlePlayerNumberChanged;
        player.SellerDecided -= HandleSellerDecided;
        if (registry != null)
            registry.Unregister(registeredNumber, transform);
    }

    void HandlePlayerNumberChanged(int playerNumber)
    {
        if (registry == null)
            return;

        registry.Unregister(registeredNumber, transform);
        registeredNumber = playerNumber;
        registry.Register(playerNumber, transform);

        if (player.IsOwner)
            registry.SetLocalPlayer(playerNumber);
    }

    void HandleSellerDecided(int sellerNumber)
    {
        if (selector != null)
            selector.NotifySellerDecided(sellerNumber);
    }
}
