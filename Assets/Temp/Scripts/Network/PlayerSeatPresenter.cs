using UnityEngine;

/// <summary>
/// 同期された席番号に合わせて、プレイヤーを椅子へ着席させる。
/// </summary>
[RequireComponent(typeof(NetworkSessionPlayer))]
public class PlayerSeatPresenter : MonoBehaviour
{
    NetworkSessionPlayer player;
    SeatRegistry seatRegistry;

    void Awake()
    {
        player = GetComponent<NetworkSessionPlayer>();
        seatRegistry = FindFirstObjectByType<SeatRegistry>();
        player.SeatIndexChanged += ApplySeat;
    }

    void OnDestroy()
    {
        player.SeatIndexChanged -= ApplySeat;
    }

    void ApplySeat(int seatIndex)
    {
        if (seatRegistry == null || !seatRegistry.TryGetSitPose(seatIndex, out Vector3 position, out Quaternion rotation))
            return;

        transform.SetPositionAndRotation(position, rotation);
    }
}
