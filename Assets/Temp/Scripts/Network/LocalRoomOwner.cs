using UnityEngine;

/// <summary>
/// 自分がこの部屋の部屋主か。SinglePlay は常に自分、マルチプレイは今ホストを務めている画面（ホストを引き継いだら引き継いだ画面）。
/// リトライで作り直した部屋で元の部屋主の戻りを待っている間は、ホストでも部屋主ではない。
/// </summary>
public class LocalRoomOwner : MonoBehaviour
{
    [SerializeField] private SinglePlaySessionBootstrap singlePlay;

    [SerializeField] private NetworkRoomState roomState;

    public bool IsOwner => singlePlay.IsActive || roomState.IsLocalOwner;
}
