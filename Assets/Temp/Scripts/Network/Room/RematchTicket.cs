using System;
using System.Collections.Generic;

/// <summary>
/// リトライ前の部屋の情報。作り直した部屋の部屋主を、元の部屋主へ戻すか先に入った人へ渡すかの判断に使う。
/// 元の部屋主とメンバーのプレイヤー ID も控え、元の部屋主のなりすましや、メンバー以外が作った偽のリトライ部屋を見分ける（オンラインのみ。Local では空）。
/// </summary>
public sealed class RematchTicket
{
    /// <summary>直前に遊んだ部屋の ID</summary>
    public string PreviousRoomId { get; }

    /// <summary>直前の部屋で自分が部屋主だったか</summary>
    public bool WasRoomOwner { get; }

    /// <summary>元の部屋主が戻らなければ、作り直した部屋の部屋主を確定させる時刻（Time.realtimeSinceStartup）</summary>
    public float OwnerDeadline { get; }

    /// <summary>直前の部屋の部屋主のプレイヤー ID。分からなければ空</summary>
    public string PreviousOwnerId { get; }

    /// <summary>直前の部屋にいたメンバーのプレイヤー ID。分からなければ空</summary>
    public IReadOnlyCollection<string> PreviousMemberIds { get; }

    public RematchTicket(string previousRoomId, bool wasRoomOwner, float ownerDeadline)
        : this(previousRoomId, wasRoomOwner, ownerDeadline, string.Empty, Array.Empty<string>())
    {
    }

    public RematchTicket(string previousRoomId, bool wasRoomOwner, float ownerDeadline, string previousOwnerId, IReadOnlyCollection<string> previousMemberIds)
    {
        PreviousRoomId = previousRoomId;
        WasRoomOwner = wasRoomOwner;
        OwnerDeadline = ownerDeadline;
        PreviousOwnerId = previousOwnerId ?? string.Empty;
        PreviousMemberIds = previousMemberIds ?? Array.Empty<string>();
    }
}
