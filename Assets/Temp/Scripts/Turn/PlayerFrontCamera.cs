using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 自分のカメラを、あるプレイヤーの正面（テーブル中央側）へ動かして映し、元の視点へ戻す。
/// 正面へ動かしている間に別のプレイヤーへ続けて動かしても、戻る先は最初に動かす前の視点のまま。
/// </summary>
public class PlayerFrontCamera : MonoBehaviour
{
    [SerializeField] private PlayerRegistry registry;

    [Tooltip("正面を決める基準（テーブル中央）")]
    [SerializeField] private Transform tableCenter;

    [Tooltip("正面へ移動する / 元の視点へ戻るのにかける秒数")]
    [SerializeField] private float moveSeconds = 0.6f;

    Camera shotCamera;
    Vector3 savedLocalPosition;
    Quaternion savedLocalRotation;

    /// <summary>元の視点から離れている（戻りきるまで）</summary>
    public bool IsAway => shotCamera != null;

    /// <summary>自分の画面を映しているカメラ</summary>
    public Camera LocalCamera => registry.TryGet(registry.LocalPlayerNumber, out Transform localPlayer)
        ? localPlayer.GetComponentInChildren<Camera>()
        : null;

    /// <summary>映す中心（target が返す位置）を、そのプレイヤーの正面から distance 離れて見る位置と向き</summary>
    public Pose FrontPose(Transform player, Vector3 target, float distance, float height)
    {
        Vector3 front = tableCenter.position - PlayerBody.HeadPosition(player);
        front.y = 0f;
        front = front.sqrMagnitude > 1e-6f ? front.normalized : player.forward;

        float scale = player.lossyScale.y;
        Vector3 position = target + front * (distance * scale) + Vector3.up * (height * scale);
        return new Pose(position, Quaternion.LookRotation(target - position, Vector3.up));
    }

    /// <summary>その人の正面へ動かす。target は毎フレーム呼び、動いている相手にも付いていく</summary>
    public IEnumerator MoveToFront(Transform player, Func<Vector3> target, float distance, float height)
    {
        if (shotCamera == null)
        {
            shotCamera = LocalCamera;
            if (shotCamera == null)
                yield break;

            savedLocalPosition = shotCamera.transform.localPosition;
            savedLocalRotation = shotCamera.transform.localRotation;
        }

        yield return Move(() => FrontPose(player, target(), distance, height));
    }

    /// <summary>正面に止めたまま seconds 秒待つ（相手が動いても付いていく）</summary>
    public IEnumerator HoldFront(Transform player, Func<Vector3> target, float distance, float height, float seconds)
    {
        float elapsed = 0f;
        yield return HoldFrontWhile(player, target, distance, height, () =>
        {
            bool holding = elapsed < seconds;
            elapsed += Time.deltaTime;
            return holding;
        });
    }

    /// <summary>keepHolding が true を返す間、正面から映し続ける</summary>
    public IEnumerator HoldFrontWhile(Transform player, Func<Vector3> target, float distance, float height, Func<bool> keepHolding)
    {
        while (keepHolding())
        {
            if (shotCamera != null)
            {
                Pose front = FrontPose(player, target(), distance, height);
                shotCamera.transform.SetPositionAndRotation(front.position, front.rotation);
            }
            yield return null;
        }
    }

    /// <summary>最初に正面へ動かす前の視点へ戻す</summary>
    public IEnumerator Return()
    {
        if (shotCamera == null)
            yield break;

        yield return Move(SavedPose);
        shotCamera.transform.SetLocalPositionAndRotation(savedLocalPosition, savedLocalRotation);
        shotCamera = null;
    }

    Pose SavedPose()
    {
        Transform parent = shotCamera.transform.parent;
        return parent != null
            ? new Pose(parent.TransformPoint(savedLocalPosition), parent.rotation * savedLocalRotation)
            : new Pose(savedLocalPosition, savedLocalRotation);
    }

    IEnumerator Move(Func<Pose> destination)
    {
        if (shotCamera == null)
            yield break;

        Transform target = shotCamera.transform;
        Vector3 fromPosition = target.position;
        Quaternion fromRotation = target.rotation;
        for (float elapsed = 0f; elapsed < moveSeconds; elapsed += Time.deltaTime)
        {
            float t = Mathf.SmoothStep(0f, 1f, elapsed / moveSeconds);
            Pose to = destination();
            target.SetPositionAndRotation(Vector3.Lerp(fromPosition, to.position, t), Quaternion.Slerp(fromRotation, to.rotation, t));
            yield return null;
        }
        Pose end = destination();
        target.SetPositionAndRotation(end.position, end.rotation);
    }
}
