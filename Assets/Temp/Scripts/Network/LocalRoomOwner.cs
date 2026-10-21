using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 自分がこの部屋の部屋主か。SinglePlay は常に自分、マルチプレイは今ホストを務めている画面（ホストを引き継いだら引き継いだ画面）。
/// </summary>
public class LocalRoomOwner : MonoBehaviour
{
    [SerializeField] private SinglePlaySessionBootstrap singlePlay;

    public bool IsOwner => singlePlay.IsActive || (NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost);
}
