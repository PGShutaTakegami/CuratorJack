using System.Collections;
using UnityEngine;

/// <summary>
/// 出品されたカードを、自分のカメラで真上から見せる演出。カードの真上へ移動して向きをカードに合わせ、
/// Return で元の位置・向きへ戻す。
/// </summary>
public class ListingCameraShot : MonoBehaviour
{
    [SerializeField] private PlayerRegistry registry;

    [Tooltip("カードの上へ移動する / 戻るのにかける秒数")]
    [SerializeField] private float moveSeconds = 0.8f;

    [Tooltip("カードの長さに対する、画面の縦に映す範囲の倍率（大きいほど引いて映す）")]
    [SerializeField] private float framing = 2.6f;

    Camera shotCamera;
    Vector3 savedLocalPosition;
    Quaternion savedLocalRotation;

    public float MoveSeconds => moveSeconds;

    /// <summary>Return で戻る先の位置（LookDown の前のカメラの位置）。演出中でなければ false</summary>
    public bool TryGetReturnPosition(out Vector3 position)
    {
        position = Vector3.zero;
        if (shotCamera == null)
            return false;

        Transform parent = shotCamera.transform.parent;
        position = parent != null ? parent.TransformPoint(savedLocalPosition) : savedLocalPosition;
        return true;
    }

    /// <summary>カードの真上へ移動し、画面の上がカードの上（forward）になる向きで見下ろす</summary>
    public IEnumerator LookDown(Transform card, float cardLength)
    {
        if (shotCamera == null)
        {
            shotCamera = FindLocalCamera();
            if (shotCamera == null)
                yield break;

            savedLocalPosition = shotCamera.transform.localPosition;
            savedLocalRotation = shotCamera.transform.localRotation;
        }

        Transform cameraTransform = shotCamera.transform;

        float halfFov = shotCamera.fieldOfView * 0.5f * Mathf.Deg2Rad;
        float distance = cardLength * 0.5f * framing / Mathf.Tan(halfFov);
        Vector3 position = card.position + Vector3.up * distance;
        Quaternion rotation = Quaternion.LookRotation(Vector3.down, card.forward);

        yield return Move(cameraTransform, position, rotation);
    }

    /// <summary>LookDown の前の位置・向きへ戻す</summary>
    public IEnumerator Return()
    {
        if (shotCamera == null)
            yield break;

        Transform cameraTransform = shotCamera.transform;
        Transform parent = cameraTransform.parent;
        TryGetReturnPosition(out Vector3 position);
        Quaternion rotation = parent != null ? parent.rotation * savedLocalRotation : savedLocalRotation;

        yield return Move(cameraTransform, position, rotation);

        cameraTransform.localPosition = savedLocalPosition;
        cameraTransform.localRotation = savedLocalRotation;
        shotCamera = null;
    }

    Camera FindLocalCamera()
    {
        return registry.TryGet(registry.LocalPlayerNumber, out Transform localPlayer)
            ? localPlayer.GetComponentInChildren<Camera>()
            : null;
    }

    IEnumerator Move(Transform target, Vector3 toPosition, Quaternion toRotation)
    {
        Vector3 fromPosition = target.position;
        Quaternion fromRotation = target.rotation;
        for (float elapsed = 0f; elapsed < moveSeconds; elapsed += Time.deltaTime)
        {
            float t = Mathf.SmoothStep(0f, 1f, elapsed / moveSeconds);
            target.SetPositionAndRotation(Vector3.Lerp(fromPosition, toPosition, t), Quaternion.Slerp(fromRotation, toRotation, t));
            yield return null;
        }
        target.SetPositionAndRotation(toPosition, toRotation);
    }
}
