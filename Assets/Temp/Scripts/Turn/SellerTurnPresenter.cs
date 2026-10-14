using System.Collections;
using UnityEngine;

/// <summary>
/// 出品者のターンの始まりの演出。出品者本人には YOURTURN をフェードで表示し、
/// 他の人はカメラを出品者へ向けながらアップにし、YOURTURN と同じ時間だけ止めてから元の向き・視野角へ戻す。
/// </summary>
public class SellerTurnPresenter : MonoBehaviour
{
    [SerializeField] private PlayerRegistry registry;

    [Tooltip("告知の背景。フェードの対象")]
    [SerializeField] private CanvasGroupFader panel;

    [SerializeField] private GameObject yourTurnMessage;

    [Tooltip("YOURTURN の表示中は隠す、同じ背景の中の告知")]
    [SerializeField] private GameObject[] otherMessages;

    [Tooltip("YOURTURN をフェードインしてからフェードアウトを始めるまでの秒数（カメラが出品者を向いている秒数）")]
    [SerializeField] private float displaySeconds = 2f;

    [Tooltip("カメラを出品者へ向ける / 戻すのにかける秒数（背景のフェードと同じ長さにする）")]
    [SerializeField] private float turnSeconds = 0.5f;

    [Tooltip("カメラの中心に映す、出品者の子オブジェクト（頭のボーン）の名前")]
    [SerializeField] private string lookTargetName = "DEF.HEAD";

    [Tooltip("出品者を向いたときの視野角の倍率（小さいほどアップになる）")]
    [Range(0.1f, 1f)]
    [SerializeField] private float zoomRatio = 0.45f;

    /// <summary>演出を行い、終わるまで待つ。</summary>
    public IEnumerator Present(int sellerNumber)
    {
        if (sellerNumber == registry.LocalPlayerNumber)
            yield return ShowYourTurn();
        else
            yield return LookAtSeller(sellerNumber);
    }

    IEnumerator ShowYourTurn()
    {
        foreach (GameObject other in otherMessages)
            other.SetActive(false);
        yourTurnMessage.SetActive(true);

        panel.GetComponent<CanvasGroup>().alpha = 0f;
        panel.gameObject.SetActive(true);

        bool isFaded = false;
        panel.FadeIn(() => isFaded = true);
        yield return new WaitUntil(() => isFaded);

        yield return new WaitForSeconds(displaySeconds);

        isFaded = false;
        panel.FadeOut(() => isFaded = true);
        yield return new WaitUntil(() => isFaded);

        yourTurnMessage.SetActive(false);
        panel.gameObject.SetActive(false);
    }

    IEnumerator LookAtSeller(int sellerNumber)
    {
        if (!registry.TryGet(registry.LocalPlayerNumber, out Transform localPlayer)
            || !registry.TryGet(sellerNumber, out Transform seller))
            yield break;

        Camera localCamera = localPlayer.GetComponentInChildren<Camera>();
        if (localCamera == null)
            yield break;

        Transform cameraTransform = localCamera.transform;
        Quaternion originalLocalRotation = cameraTransform.localRotation;
        Quaternion original = cameraTransform.rotation;
        Quaternion toSeller = Quaternion.LookRotation(PlayerBody.HeadPosition(seller, lookTargetName) - cameraTransform.position, Vector3.up);
        float originalFov = localCamera.fieldOfView;
        float zoomedFov = originalFov * zoomRatio;

        yield return Turn(localCamera, original, toSeller, originalFov, zoomedFov);
        yield return new WaitForSeconds(displaySeconds);
        yield return Turn(localCamera, toSeller, original, zoomedFov, originalFov);

        cameraTransform.localRotation = originalLocalRotation;
        localCamera.fieldOfView = originalFov;
    }

    /// <summary>向きと視野角を同時に変える（向きながらアップ / 戻りながら引く）</summary>
    IEnumerator Turn(Camera target, Quaternion from, Quaternion to, float fromFov, float toFov)
    {
        Transform targetTransform = target.transform;
        for (float elapsed = 0f; elapsed < turnSeconds; elapsed += Time.deltaTime)
        {
            float t = Mathf.SmoothStep(0f, 1f, elapsed / turnSeconds);
            targetTransform.rotation = Quaternion.Slerp(from, to, t);
            target.fieldOfView = Mathf.Lerp(fromFov, toFov, t);
            yield return null;
        }
        targetTransform.rotation = to;
        target.fieldOfView = toFov;
    }
}
