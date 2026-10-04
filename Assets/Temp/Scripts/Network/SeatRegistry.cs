using UnityEngine;

/// <summary>
/// テーブルを囲む椅子と、椅子に対する着席姿勢（椅子ローカルの位置・回転）を保持する。
/// seats はテーブルを一周する順に並べる（向かいの席 = 半周先）。
/// </summary>
public class SeatRegistry : MonoBehaviour
{
    [SerializeField] private Transform[] seats;

    [Tooltip("Player1 が座る席番号")]
    [SerializeField] private int firstSeat;

    [Tooltip("椅子ローカル空間でのプレイヤーの位置")]
    [SerializeField] private Vector3 sitLocalPosition;

    [Tooltip("椅子ローカル空間でのプレイヤーの回転")]
    [SerializeField] private Quaternion sitLocalRotation = Quaternion.identity;

    public int SeatCount => seats != null ? seats.Length : 0;

    public int FirstSeat => firstSeat;

    public bool TryGetSitPose(int seatIndex, out Vector3 position, out Quaternion rotation)
    {
        if (seats == null || seatIndex < 0 || seatIndex >= seats.Length || seats[seatIndex] == null)
        {
            position = default;
            rotation = Quaternion.identity;
            return false;
        }

        Transform seat = seats[seatIndex];
        position = seat.TransformPoint(sitLocalPosition);
        rotation = seat.rotation * sitLocalRotation;
        return true;
    }
}
