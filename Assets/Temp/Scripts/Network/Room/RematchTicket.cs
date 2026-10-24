/// <summary>
/// リトライ前の部屋の情報。作り直した部屋の部屋主を、元の部屋主へ戻すか先に入った人へ渡すかの判断に使う。
/// </summary>
public sealed class RematchTicket
{
    /// <summary>直前に遊んだ部屋の ID</summary>
    public string PreviousRoomId { get; }

    /// <summary>直前の部屋で自分が部屋主だったか</summary>
    public bool WasRoomOwner { get; }

    /// <summary>元の部屋主が戻らなければ、作り直した部屋の部屋主を確定させる時刻（Time.realtimeSinceStartup）</summary>
    public float OwnerDeadline { get; }

    public RematchTicket(string previousRoomId, bool wasRoomOwner, float ownerDeadline)
    {
        PreviousRoomId = previousRoomId;
        WasRoomOwner = wasRoomOwner;
        OwnerDeadline = ownerDeadline;
    }
}
