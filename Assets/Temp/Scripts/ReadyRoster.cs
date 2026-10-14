using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ゲーム開始前の準備状況（参加人数と、プレイヤーごとの準備完了）をまとめる。
/// 自分の準備状態の変更は、プレイ形式（SinglePlay / ネットワーク）ごとに登録された処理へ任せる。
/// </summary>
public class ReadyRoster : MonoBehaviour
{
    readonly Dictionary<int, bool> readyByPlayer = new Dictionary<int, bool>();
    Action<bool> localReadyHandler;

    public int TotalPlayers { get; private set; }

    public int ReadyCount
    {
        get
        {
            int count = 0;
            foreach (bool ready in readyByPlayer.Values)
            {
                if (ready)
                    count++;
            }

            return count;
        }
    }

    public bool IsAllReady => TotalPlayers > 0 && ReadyCount >= TotalPlayers;

    public event Action Changed;

    public void SetTotal(int totalPlayers)
    {
        TotalPlayers = totalPlayers;
        Changed?.Invoke();
    }

    public void SetReady(int playerNumber, bool ready)
    {
        readyByPlayer[playerNumber] = ready;
        Changed?.Invoke();
    }

    public void Remove(int playerNumber)
    {
        if (readyByPlayer.Remove(playerNumber))
            Changed?.Invoke();
    }

    public void SetLocalReadyHandler(Action<bool> handler)
    {
        localReadyHandler = handler;
    }

    public void RequestLocalReady(bool ready)
    {
        localReadyHandler?.Invoke(ready);
    }
}
