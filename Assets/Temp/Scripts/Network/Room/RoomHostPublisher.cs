using Unity.Netcode;
using UnityEngine;

/// <summary>
/// ホスト中の部屋情報（現在人数）を定期的に公開し、終了時に一覧から取り下げる。
/// </summary>
public class RoomHostPublisher : MonoBehaviour
{
    [SerializeField] private NetworkManager networkManager;

    [Tooltip("部屋情報を公開し直す間隔（秒）")]
    [SerializeField] private float publishInterval = 0.5f;

    IRoomDirectory directory;
    RoomInfo room;
    float publishTimer;

    public void Begin(RoomInfo hostedRoom, IRoomDirectory roomDirectory)
    {
        room = hostedRoom;
        directory = roomDirectory;
        publishTimer = 0f;
        Publish();
    }

    public void End()
    {
        if (room == null)
            return;

        directory.Remove(room.roomId);
        room = null;
    }

    void Update()
    {
        if (room == null)
            return;

        publishTimer += Time.unscaledDeltaTime;
        if (publishTimer < publishInterval)
            return;

        publishTimer = 0f;
        Publish();
    }

    void OnApplicationQuit()
    {
        End();
    }

    void OnDestroy()
    {
        End();
    }

    void Publish()
    {
        room.playerCount = networkManager.IsListening ? networkManager.ConnectedClientsIds.Count : 0;
        directory.Publish(room);
    }
}
