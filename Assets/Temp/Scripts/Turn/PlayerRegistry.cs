using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ゲームに参加しているプレイヤーを、プレイヤー番号から引けるように保持する。
/// 自分（この画面を操作している人）のプレイヤー番号も保持する。
/// </summary>
public class PlayerRegistry : MonoBehaviour
{
    public const int NoPlayer = 0;

    readonly Dictionary<int, Transform> players = new Dictionary<int, Transform>();

    public int LocalPlayerNumber { get; private set; } = NoPlayer;

    public IReadOnlyCollection<int> PlayerNumbers => players.Keys;

    public void Register(int playerNumber, Transform player)
    {
        if (playerNumber != NoPlayer && player != null)
            players[playerNumber] = player;
    }

    /// <summary>登録されているのが player 本人のときだけ外す。</summary>
    public void Unregister(int playerNumber, Transform player)
    {
        if (players.TryGetValue(playerNumber, out Transform registered) && registered == player)
            players.Remove(playerNumber);
    }

    public void SetLocalPlayer(int playerNumber)
    {
        LocalPlayerNumber = playerNumber;
    }

    public bool TryGet(int playerNumber, out Transform player)
    {
        return players.TryGetValue(playerNumber, out player) && player != null;
    }
}
