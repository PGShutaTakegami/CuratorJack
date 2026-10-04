using Unity.Netcode;
using UnityEngine;

/// <summary>
/// プレイヤーに内蔵されたカメラ / AudioListener を、自分のプレイヤーのものだけ有効にする。
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
        SetActive(IsOwner);
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
