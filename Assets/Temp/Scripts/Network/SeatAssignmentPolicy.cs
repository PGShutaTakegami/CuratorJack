using System.Collections.Generic;

/// <summary>
/// プレイヤー番号から座る席を決める。
/// Player1 は既定席、Player2 は Player1 の向かい（2 人なら向かい合う）、Player3 / 4 は空席からランダム。
/// </summary>
public sealed class SeatAssignmentPolicy
{
    public const int NoSeat = -1;

    readonly int seatCount;
    readonly int firstSeat;
    readonly System.Random random;

    public SeatAssignmentPolicy(int seatCount, int firstSeat, System.Random random)
    {
        this.seatCount = seatCount;
        this.firstSeat = firstSeat;
        this.random = random;
    }

    public int OppositeOf(int seat)
    {
        return (seat + seatCount / 2) % seatCount;
    }

    /// <param name="seatByPlayer">着席済みのプレイヤー番号 → 席番号</param>
    public int ChooseSeat(int playerNumber, IReadOnlyDictionary<int, int> seatByPlayer)
    {
        if (seatCount <= 0)
            return NoSeat;

        if (playerNumber == 1 || playerNumber == 2)
        {
            int partner = playerNumber == 1 ? 2 : 1;
            int preferred = seatByPlayer.TryGetValue(partner, out int partnerSeat)
                ? OppositeOf(partnerSeat)
                : firstSeat;

            if (IsFree(preferred, seatByPlayer))
                return preferred;
        }

        return ChooseRandomFreeSeat(seatByPlayer);
    }

    int ChooseRandomFreeSeat(IReadOnlyDictionary<int, int> seatByPlayer)
    {
        var freeSeats = new List<int>();
        for (int seat = 0; seat < seatCount; seat++)
        {
            if (IsFree(seat, seatByPlayer))
                freeSeats.Add(seat);
        }

        return freeSeats.Count == 0 ? NoSeat : freeSeats[random.Next(freeSeats.Count)];
    }

    static bool IsFree(int seat, IReadOnlyDictionary<int, int> seatByPlayer)
    {
        foreach (int occupied in seatByPlayer.Values)
        {
            if (occupied == seat)
                return false;
        }

        return true;
    }
}
