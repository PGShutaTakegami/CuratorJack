using Unity.Netcode;
using UnityEngine;

/// <summary>
/// プレイヤーに内蔵されたカメラ / AudioListener を、自分のプレイヤーのものだけ有効にする。
/// ホストが持つ CPU のプレイヤーは自分のものではないため、持ち主ではなく自分の接続のプレイヤーかで判断する。
/// </summary>
public class OwnerCameraActivator : NetworkBehaviour
{
    Camera[] cameras;
    AudioListener[] listeners;

    void Awake()
    {
        cameras = GetComponentsInChildren<Camera>(true);
        listeners = GetComponentsInChildren<AudioListener>(true);
        SetActive(false);
    }

    public override void OnNetworkSpawn()
    {
        SetActive(IsLocalPlayer);
    }

    public override void OnNetworkDespawn()
    {
        SetActive(false);
    }

    void SetActive(bool active)
    {
        foreach (var cam in cameras)
            cam.enabled = active;
        foreach (var listener in listeners)
            listener.enabled = active;
    }
}
