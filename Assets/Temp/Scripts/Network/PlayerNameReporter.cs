using UnityEngine;

/// <summary>
/// 自分のプレイヤーに番号が割り当てられたら、ロビーで入力した名前をサーバーへ送る。
/// </summary>
[RequireComponent(typeof(NetworkSessionPlayer))]
public class PlayerNameReporter : MonoBehaviour
{
    NetworkSessionPlayer player;
    bool isReported;

    void Awake()
    {
        player = GetComponent<NetworkSessionPlayer>();
        player.PlayerNumberChanged += HandlePlayerNumberChanged;
    }

    void OnDestroy()
    {
        player.PlayerNumberChanged -= HandlePlayerNumberChanged;
    }

    void HandlePlayerNumberChanged(int playerNumber)
    {
        if (isReported || playerNumber == NetworkSessionPlayer.UnassignedNumber || !player.IsLocalPlayer)
            return;

        isReported = true;
        player.RequestDisplayName(PlayerNameContext.Current);
    }
}
