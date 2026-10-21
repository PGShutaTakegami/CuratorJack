/// <summary>
/// ルームにいるプレイヤー 1 人分（自分 / ほかの人 / コンピューター）。
/// </summary>
public readonly struct RoomMember
{
    public int PlayerNumber { get; }

    public bool IsCpu { get; }

    /// <summary>この画面を操作している本人か</summary>
    public bool IsLocal { get; }

    public RoomMember(int playerNumber, bool isCpu, bool isLocal)
    {
        PlayerNumber = playerNumber;
        IsCpu = isCpu;
        IsLocal = isLocal;
    }
}
