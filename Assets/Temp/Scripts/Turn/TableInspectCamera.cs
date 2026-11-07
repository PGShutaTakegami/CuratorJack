using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

/// <summary>
/// プレイヤーの手元の置き場（落札したカードの山と半券）を、見るための専用カメラで上から見せてから元の視点へ戻す。
/// 自分のカメラは動かさないため、自分のカメラを動かす演出（動かす前の姿勢へ戻す）を邪魔しない。
/// 見ている途中で演出によって自分のカメラが動き出したら、すぐに戻る。クリックでも早く戻れる。
/// </summary>
public class TableInspectCamera : MonoBehaviour
{
    [SerializeField] private PlayerRegistry registry;

    [Tooltip("置き場の位置と向き")]
    [SerializeField] private PlayerCardPiles piles;

    [Tooltip("山の中心から、その人の席から見て右へずらして映す距離（右に置く半券も映す）")]
    [SerializeField] private float sideOffset = 0.25f;

    [Tooltip("映す中心を、山から自分の側へずらす距離（山を画面の上寄りに映し、下の入札の UI と重ねない）")]
    [FormerlySerializedAs("seatOffset")]
    [SerializeField] private float viewerOffset = 0.2f;

    [Tooltip("映す中心からの高さ")]
    [SerializeField] private float height = 1.5f;

    [Tooltip("映す中心から、自分の側へ引く距離（0 で真上から）")]
    [SerializeField] private float backOffset = 0.25f;

    [Tooltip("置き場へ移動する / 元の視点へ戻るのにかける秒数")]
    [SerializeField] private float moveSeconds = 0.6f;

    [Tooltip("置き場を見せ続ける秒数")]
    [SerializeField] private float holdSeconds = 2.5f;

    Camera viewCamera;
    Camera localCamera;
    Vector3 homeLocalPosition;
    Quaternion homeLocalRotation;
    float homeFieldOfView;
    Coroutine running;

    public bool IsInspecting => running != null;

    /// <summary>自分の画面を映しているカメラ（見つからなければ null）</summary>
    public Camera LocalCamera => TryGetLocalCamera(out Camera found) ? found : null;

    /// <summary>見ている途中でなく、自分のカメラが演出で動いていない</summary>
    public bool CanInspect => !IsInspecting && TryGetLocalCamera(out Camera found) && IsHome(found);

    /// <summary>そのプレイヤーの置き場を見せる（見せられないときは何もしない）</summary>
    public void Inspect(int playerNumber)
    {
        if (!CanInspect || !piles.TryGetSpot(playerNumber, out Pose spot))
            return;

        running = StartCoroutine(Run(LookPose(spot, localCamera.transform.position)));
    }

    void OnDisable()
    {
        if (running != null)
            StopCoroutine(running);
        running = null;
        if (viewCamera != null)
            viewCamera.enabled = false;
    }

    IEnumerator Run(Pose look)
    {
        BeginView();

        Func<bool> interrupted = () => localCamera == null || !IsHome(localCamera);
        bool reached = false;
        yield return Move(look, () => homeFieldOfView, interrupted, () => reached = true);

        if (reached)
        {
            for (float elapsed = 0f; elapsed < holdSeconds && !interrupted() && !Clicked(); elapsed += Time.deltaTime)
                yield return null;
        }

        if (localCamera != null)
            yield return Move(() => PoseOf(localCamera.transform), () => localCamera.fieldOfView, () => localCamera == null, null);

        viewCamera.enabled = false;
        running = null;
    }

    /// <summary>
    /// 自分のカメラから山へ向かう水平の向きを画面の上にして見下ろす（その人は画面の上側に映り、今の向きからの回転が最小限になる）
    /// </summary>
    Pose LookPose(Pose spot, Vector3 viewer)
    {
        Vector3 pile = spot.position + spot.rotation * Vector3.right * sideOffset;
        Vector3 away = pile - viewer;
        away.y = 0f;
        away = away.sqrMagnitude > 1e-6f ? away.normalized : -(spot.rotation * Vector3.forward);

        Vector3 target = pile - away * viewerOffset;
        Vector3 position = target - away * backOffset + Vector3.up * height;
        return new Pose(position, Quaternion.LookRotation(target - position, away));
    }

    void BeginView()
    {
        if (viewCamera == null)
        {
            var go = new GameObject("TableInspectView");
            go.transform.SetParent(transform, false);
            viewCamera = go.AddComponent<Camera>();
        }

        viewCamera.CopyFrom(localCamera);
        viewCamera.depth = localCamera.depth + 1f;

        UniversalAdditionalCameraData source = localCamera.GetUniversalAdditionalCameraData();
        UniversalAdditionalCameraData target = viewCamera.GetUniversalAdditionalCameraData();
        target.renderPostProcessing = source.renderPostProcessing;
        target.antialiasing = source.antialiasing;
        target.antialiasingQuality = source.antialiasingQuality;
        target.renderShadows = source.renderShadows;
        target.volumeLayerMask = source.volumeLayerMask;
        target.stopNaN = source.stopNaN;
        target.dithering = source.dithering;

        viewCamera.enabled = true;
    }

    IEnumerator Move(Pose destination, Func<float> fieldOfView, Func<bool> interrupted, Action reached)
    {
        return Move(() => destination, fieldOfView, interrupted, reached);
    }

    /// <summary>destination へ動かす。interrupted が true になったら、その場で止める（reached は呼ばない）</summary>
    IEnumerator Move(Func<Pose> destination, Func<float> fieldOfView, Func<bool> interrupted, Action reached)
    {
        Transform view = viewCamera.transform;
        Pose from = PoseOf(view);
        float fromFieldOfView = viewCamera.fieldOfView;
        for (float elapsed = 0f; elapsed < moveSeconds; elapsed += Time.deltaTime)
        {
            if (interrupted())
                yield break;

            float t = Mathf.SmoothStep(0f, 1f, elapsed / moveSeconds);
            Pose to = destination();
            view.SetPositionAndRotation(Vector3.Lerp(from.position, to.position, t), Quaternion.Slerp(from.rotation, to.rotation, t));
            viewCamera.fieldOfView = Mathf.Lerp(fromFieldOfView, fieldOfView(), t);
            yield return null;
        }

        if (interrupted())
            yield break;

        Pose end = destination();
        view.SetPositionAndRotation(end.position, end.rotation);
        viewCamera.fieldOfView = fieldOfView();
        reached?.Invoke();
    }

    /// <summary>自分のカメラ。初めて見つけたときの姿勢を、演出で動いていないときの姿勢として覚える</summary>
    bool TryGetLocalCamera(out Camera found)
    {
        found = registry.TryGet(registry.LocalPlayerNumber, out Transform localPlayer)
            ? localPlayer.GetComponentInChildren<Camera>()
            : null;
        if (found == null)
            return false;

        if (found != localCamera && !IsInspecting)
        {
            localCamera = found;
            homeLocalPosition = found.transform.localPosition;
            homeLocalRotation = found.transform.localRotation;
            homeFieldOfView = found.fieldOfView;
        }
        return found == localCamera;
    }

    bool IsHome(Camera target)
    {
        Transform t = target.transform;
        return (t.localPosition - homeLocalPosition).sqrMagnitude < 1e-6f
            && Quaternion.Angle(t.localRotation, homeLocalRotation) < 0.01f
            && Mathf.Abs(target.fieldOfView - homeFieldOfView) < 0.01f;
    }

    static bool Clicked()
    {
        Mouse mouse = Mouse.current;
        return mouse != null && mouse.leftButton.wasPressedThisFrame;
    }

    static Pose PoseOf(Transform target)
    {
        return new Pose(target.position, target.rotation);
    }
}
