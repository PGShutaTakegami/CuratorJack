using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 自分のプレイヤーだけが Space で決定入力を送る。
/// </summary>
[RequireComponent(typeof(NetworkSessionPlayer))]
public class PlayerPushInput : MonoBehaviour
{
    NetworkSessionPlayer player;

    void Awake()
    {
        player = GetComponent<NetworkSessionPlayer>();
    }

    void Update()
    {
        if (!player.IsSpawned || !player.IsOwner)
            return;

        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
            player.RequestPush();
    }
}
