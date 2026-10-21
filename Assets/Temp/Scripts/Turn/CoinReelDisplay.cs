using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 所持コインをリールの枠（桁の数だけ）で表示する画面の UI。3D 空間の位置（胸など）に重ねて表示し、
/// 数字を一定の間隔で少しずつ変えて、変わった桁を巻いて切り替える。
/// 枠の数は表示し始めた値の桁数より減らさない（減った上の桁は 0 を表示する）。
/// </summary>
public class CoinReelDisplay : MonoBehaviour
{
    [Tooltip("桁の枠。最初に置いた枠の数より桁が多いときだけ左に枠が足される")]
    [SerializeField] private ReelNumberView reels;

    [SerializeField] private CanvasGroup group;

    [SerializeField] private float fadeSeconds = 0.25f;

    [Tooltip("前の金額から後の金額まで数字を変えるのにかける秒数")]
    [SerializeField] private float countSeconds = 1.2f;

    [Tooltip("数字を変える回数の上限（差が大きいときは飛ばして変える）")]
    [SerializeField] private int maxCountSteps = 20;

    [Tooltip("重ねる位置が画面に映っていないときに置く位置（画面の割合。左下が 0）")]
    [SerializeField] private Vector2 fallbackViewport = new Vector2(0.5f, 0.3f);

    Camera viewCamera;
    Func<Vector3> worldAnchor;
    bool shown;
    int value;

    /// <summary>
    /// 数字を巻かずに出してフェードで表示する。worldAnchor の位置を viewCamera で映した所に重ねる
    /// （worldAnchor が null か映っていなければ fallbackViewport の位置）
    /// </summary>
    public void Show(int startValue, Camera camera, Func<Vector3> anchor)
    {
        viewCamera = camera;
        worldAnchor = anchor;
        value = startValue;
        group.alpha = 0f;
        group.blocksRaycasts = false;
        gameObject.SetActive(true);
        reels.SetMinimumDigits(Mathf.Max(0, startValue).ToString().Length);
        reels.SetValue(startValue, false);
        shown = true;
        Follow();
    }

    /// <summary>今の数字から target まで、少しずつ変えて巻いていく</summary>
    public IEnumerator CountTo(int target)
    {
        int from = value;
        int steps = Mathf.Clamp(Mathf.Abs(target - from), 1, Mathf.Max(1, maxCountSteps));
        float interval = countSeconds / steps;
        for (int i = 1; i <= steps; i++)
        {
            yield return new WaitForSeconds(interval);
            value = Mathf.RoundToInt(Mathf.Lerp(from, target, (float)i / steps));
            reels.SetValue(value, true);
        }
    }

    public void Hide()
    {
        shown = false;
    }

    public bool IsVisible => shown || group.alpha > 0f;

    void LateUpdate()
    {
        group.alpha = Mathf.MoveTowards(group.alpha, shown ? 1f : 0f, fadeSeconds > 0f ? Time.deltaTime / fadeSeconds : 1f);
        if (!shown && group.alpha <= 0f)
        {
            gameObject.SetActive(false);
            return;
        }
        Follow();
    }

    void Follow()
    {
        var rect = (RectTransform)transform;
        var parent = (RectTransform)rect.parent;
        Canvas canvas = GetComponentInParent<Canvas>().rootCanvas;
        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

        Vector3 screen = new Vector3(fallbackViewport.x * Screen.width, fallbackViewport.y * Screen.height, 1f);
        if (viewCamera != null && worldAnchor != null)
        {
            Vector3 projected = viewCamera.WorldToScreenPoint(worldAnchor());
            if (projected.z > 0f)
                screen = projected;
        }

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, uiCamera, out Vector2 local))
            rect.localPosition = new Vector3(local.x, local.y, 0f);
    }
}
