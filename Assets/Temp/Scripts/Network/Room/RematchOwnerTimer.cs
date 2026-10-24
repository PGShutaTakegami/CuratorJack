/// <summary>
/// 作り直した部屋で、元の部屋主の戻りを待つ期限。
/// 元の部屋主が何もしなければリザルトから一定時間で、元の部屋が閉じたら（タイトルへ戻った / リトライした）そこから少しだけ待って期限にする。
/// </summary>
public sealed class RematchOwnerTimer
{
    readonly float closedGraceSeconds;
    float deadline;

    public bool HasPreviousRoomClosed { get; private set; }

    /// <summary>ticket が無い（リトライを経由していない）ときは、待たずに期限切れとする。</summary>
    public RematchOwnerTimer(RematchTicket ticket, float closedGraceSeconds, float now)
    {
        this.closedGraceSeconds = closedGraceSeconds;
        deadline = ticket != null ? ticket.OwnerDeadline : now;
    }

    /// <summary>元の部屋主がリトライしていれば入ってくる間だけ待つため、元の期限より延びることもある。</summary>
    public void NotifyPreviousRoomClosed(float now)
    {
        if (HasPreviousRoomClosed)
            return;

        HasPreviousRoomClosed = true;
        deadline = now + closedGraceSeconds;
    }

    public bool IsExpired(float now) => now >= deadline;
}
