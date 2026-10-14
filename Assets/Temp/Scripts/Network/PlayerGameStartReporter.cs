using UnityEngine;

/// <summary>
/// ホストから届いたゲーム開始の通知を、シーンの GameStartGate へ伝える。
/// </summary>
[RequireComponent(typeof(NetworkSessionPlayer))]
public class PlayerGameStartReporter : MonoBehaviour
{
    NetworkSessionPlayer player;
    GameStartGate startGate;

    void Awake()
    {
        player = GetComponent<NetworkSessionPlayer>();
        startGate = FindFirstObjectByType<GameStartGate>();
        player.GameStarted += HandleGameStarted;
    }

    void OnDestroy()
    {
        player.GameStarted -= HandleGameStarted;
    }

    void HandleGameStarted()
    {
        if (startGate != null)
            startGate.NotifyStarted();
    }
}
