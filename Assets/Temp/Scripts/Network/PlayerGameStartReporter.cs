using UnityEngine;

/// <summary>
/// ホストから届いたゲーム開始の通知を、シーンの NetworkMatchBootstrap へ伝える（ターンの進行を始めてからゲームを開始する）。
/// </summary>
[RequireComponent(typeof(NetworkSessionPlayer))]
public class PlayerGameStartReporter : MonoBehaviour
{
    NetworkSessionPlayer player;
    NetworkMatchBootstrap match;

    void Awake()
    {
        player = GetComponent<NetworkSessionPlayer>();
        match = FindFirstObjectByType<NetworkMatchBootstrap>();
        player.GameStarted += HandleGameStarted;
    }

    void OnDestroy()
    {
        player.GameStarted -= HandleGameStarted;
    }

    void HandleGameStarted(int seed)
    {
        if (match != null)
            match.Begin(seed);
    }
}
