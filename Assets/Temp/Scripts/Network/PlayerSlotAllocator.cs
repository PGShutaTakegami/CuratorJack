using System.Collections.Generic;

/// <summary>
/// 接続クライアントにプレイヤー番号（1 始まり）を割り当てる。空いている最小番号を使う。
/// </summary>
public sealed class PlayerSlotAllocator
{
    readonly int maxPlayers;
    readonly Dictionary<ulong, int> numberByClient = new Dictionary<ulong, int>();

    public PlayerSlotAllocator(int maxPlayers)
    {
        this.maxPlayers = maxPlayers;
    }

    public int MaxPlayers => maxPlayers;

    public int Count => numberByClient.Count;

    public bool HasFreeSlot => numberByClient.Count < maxPlayers;

    public bool TryGetNumber(ulong clientId, out int playerNumber)
    {
        return numberByClient.TryGetValue(clientId, out playerNumber);
    }

    public bool TryAssign(ulong clientId, out int playerNumber)
    {
        if (numberByClient.TryGetValue(clientId, out playerNumber))
            return true;

        for (int number = 1; number <= maxPlayers; number++)
        {
            if (numberByClient.ContainsValue(number))
                continue;

            numberByClient.Add(clientId, number);
            playerNumber = number;
            return true;
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
