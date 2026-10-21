using System.Collections.Generic;

/// <summary>
/// 接続クライアントとホストが動かす CPU にプレイヤー番号（1 始まり）を割り当てる。空いている最小番号を使う。
/// 同時に割り当てる人数（部屋の人数）はあとから変えられる。番号は席の数を超えない。
/// </summary>
public sealed class PlayerSlotAllocator
{
    readonly int numberLimit;
    readonly Dictionary<ulong, int> numberByClient = new Dictionary<ulong, int>();
    readonly HashSet<int> cpuNumbers = new HashSet<int>();
    int maxPlayers;

    /// <param name="maxPlayers">同時に割り当てる人数</param>
    /// <param name="numberLimit">割り当てる番号の上限（席の数）</param>
    public PlayerSlotAllocator(int maxPlayers, int numberLimit)
    {
        this.numberLimit = numberLimit;
        SetMaxPlayers(maxPlayers);
    }

    public int MaxPlayers => maxPlayers;

    /// <summary>接続クライアントと CPU を合わせた人数</summary>
    public int Count => numberByClient.Count + cpuNumbers.Count;

    public bool HasFreeSlot => Count < maxPlayers;

    /// <summary>番号の上限を超える人数にはしない</summary>
    public void SetMaxPlayers(int players)
    {
        maxPlayers = System.Math.Min(players, numberLimit);
    }

    public bool TryGetNumber(ulong clientId, out int playerNumber)
    {
        return numberByClient.TryGetValue(clientId, out playerNumber);
    }

    public bool TryGetClient(int playerNumber, out ulong clientId)
    {
        foreach (KeyValuePair<ulong, int> pair in numberByClient)
        {
            if (pair.Value == playerNumber)
            {
                clientId = pair.Key;
                return true;
            }
        }

        clientId = 0;
        return false;
    }

    public bool TryAssign(ulong clientId, out int playerNumber)
    {
        if (numberByClient.TryGetValue(clientId, out playerNumber))
            return true;

        if (!TryFindFreeNumber(out playerNumber))
            return false;

        numberByClient.Add(clientId, playerNumber);
        return true;
    }

    public bool TryAssignCpu(out int playerNumber)
    {
        if (!TryFindFreeNumber(out playerNumber))
            return false;

        cpuNumbers.Add(playerNumber);
        return true;
    }

    public bool IsCpu(int playerNumber) => cpuNumbers.Contains(playerNumber);

    public bool ReleaseCpu(int playerNumber) => cpuNumbers.Remove(playerNumber);

    bool TryFindFreeNumber(out int playerNumber)
    {
        if (HasFreeSlot)
        {
            for (int number = 1; number <= numberLimit; number++)
            {
                if (numberByClient.ContainsValue(number) || cpuNumbers.Contains(number))
                    continue;

                playerNumber = number;
                return true;
            }
        }

        playerNumber = 0;
        return false;
    }

    public bool TryRelease(ulong clientId, out int playerNumber)
    {
        if (!numberByClient.TryGetValue(clientId, out playerNumber))
            return false;

        numberByClient.Remove(clientId);
        return true;
    }
}
