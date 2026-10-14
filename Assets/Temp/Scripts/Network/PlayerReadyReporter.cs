using UnityEngine;

/// <summary>
/// 同期されたプレイヤーの準備状態をシーンの ReadyRoster へ反映する。
/// 自分のプレイヤーなら、準備状態の変更をサーバーへ送る処理として登録する。
/// </summary>
[RequireComponent(typeof(NetworkSessionPlayer))]
public class PlayerReadyReporter : MonoBehaviour
{
    NetworkSessionPlayer player;
    ReadyRoster roster;
    int reportedNumber = NetworkSessionPlayer.UnassignedNumber;
    bool isLocalHandler;

    void Awake()
    {
        player = GetComponent<NetworkSessionPlayer>();
        roster = FindFirstObjectByType<ReadyRoster>();
        player.PlayerNumberChanged += HandlePlayerNumberChanged;
        player.ReadyChanged += HandleReadyChanged;
    }

    void OnDestroy()
    {
        player.PlayerNumberChanged -= HandlePlayerNumberChanged;
        player.ReadyChanged -= HandleReadyChanged;
        if (roster == null)
            return;

        if (reportedNumber != NetworkSessionPlayer.UnassignedNumber)
            roster.Remove(reportedNumber);
        if (isLocalHandler)
            roster.SetLocalReadyHandler(null);
    }

    void HandlePlayerNumberChanged(int playerNumber)
    {
        if (roster == null)
            return;

        if (reportedNumber != NetworkSessionPlayer.UnassignedNumber)
            roster.Remove(reportedNumber);

        reportedNumber = playerNumber;
        Report();

        if (playerNumber != NetworkSessionPlayer.UnassignedNumber && player.IsOwner && !isLocalHandler)
        {
            roster.SetLocalReadyHandler(player.RequestReady);
            isLocalHandler = true;
        }
    }

    void HandleReadyChanged(bool ready)
    {
        Report();
    }

    void Report()
    {
        if (roster != null && reportedNumber != NetworkSessionPlayer.UnassignedNumber)
            roster.SetReady(reportedNumber, player.IsReady);
    }
}
