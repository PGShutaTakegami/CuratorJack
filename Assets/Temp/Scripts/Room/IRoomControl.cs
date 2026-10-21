using System;
using System.Collections.Generic;

/// <summary>
/// ルーム設定で変える部屋の状態と操作。プレイ形式（SinglePlay / マルチプレイ）ごとに受け持つ。
/// 人数・待ち時間・CPU で埋めるはゲーム開始前だけ、キックはゲーム中も行える。
/// </summary>
public interface IRoomControl
{
    /// <summary>このプレイ形式で遊んでいるか</summary>
    bool IsActive { get; }

    /// <summary>部屋の人数（空き枠を含む）</summary>
    int Capacity { get; }

    /// <summary>選べる人数の上限（席の数）</summary>
    int MaxCapacity { get; }

    /// <summary>部屋にいるプレイヤー。プレイヤー番号の小さい順</summary>
    IReadOnlyList<RoomMember> Members { get; }

    /// <summary>出品・落札の待ち時間（秒）。RoomWaitTime.Infinite は時間切れなし</summary>
    int WaitSeconds { get; }

    bool CanChangeWaitTime { get; }

    bool CanFillWithCpu { get; }

    event Action Changed;

    /// <summary>今いる人数より少なくはできない</summary>
    bool SetCapacity(int capacity);

    void SetWaitSeconds(int seconds);

    /// <summary>空き枠を 1 つ CPU で埋める</summary>
    bool FillWithCpu();

    /// <summary>ゲーム開始前は部屋から外して空き枠にし、ゲーム中はリタイアさせる</summary>
    void Kick(int playerNumber);
}
