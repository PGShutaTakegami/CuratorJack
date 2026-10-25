using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>矢印で対象を指す向き</summary>
public enum TutorialPointerSide
{
    /// <summary>上側から指す。上に置く場所がなければ横から指す</summary>
    Auto,

    /// <summary>左側から指す（上に置くと別の表示に重なる対象に使う）</summary>
    Left
}

/// <summary>
/// 案内で操作してほしい UI を指す矢印。対象の上側から下向きに指し、指す向きに弾ませる。
/// 対象が画面の上のほうにあるときは、横から指す。対象が動いても毎フレーム追いかける。
/// 対象を複数渡すと、表示されている最初の 1 つを指す（出たり消えたりする UI と、その元の UI を渡す）。
/// 対象が画像を持たない入れ物なら、中に表示されている画像をまとめた範囲を指す。
/// 画面のほとんどを覆う対象（ウィンドウの背景など）は指しても分からないため、矢印を出さない。
/// 矢印の画像は下向きで、ピボットを先端（下端の中央）に置いておく。
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class TutorialPointer : MonoBehaviour
{
    [SerializeField] private RectTransform arrow;

    [Tooltip("矢印の先端と対象のふちの間隔")]
    [SerializeField] private float gap = 16f;

    [Tooltip("弾む高さ")]
    [SerializeField] private float bounceHeight = 22f;

    [Tooltip("1 秒あたりに弾む回数")]
    [SerializeField] private float bouncesPerSecond = 1.4f;

    [Tooltip("対象の上端が画面のこの高さ（下から 0〜1）より上なら、横から指す")]
    [FormerlySerializedAs("pointFromBelowAbove")]
    [Range(0f, 1f)]
    [SerializeField] private float pointFromSideAbove = 0.7f;

    [Tooltip("対象が画面の幅・高さともにこの割合以上を覆うなら、矢印を出さない")]
    [Range(0f, 1f)]
    [SerializeField] private float ignoreCoverage = 0.9f;

    readonly Vector3[] corners = new Vector3[4];
    readonly List<Graphic> graphics = new List<Graphic>();
    readonly List<RectTransform> targets = new List<RectTransform>();
    TutorialPointerSide side;
    RectTransform area;

    void Awake()
    {
        area = (RectTransform)transform;
        arrow.gameObject.SetActive(false);
    }

    public void PointAt(TutorialPointerSide newSide, IEnumerable<RectTransform> newTargets)
    {
        side = newSide;
        targets.Clear();
        foreach (RectTransform target in newTargets)
        {
            if (target != null)
                targets.Add(target);
        }
        LateUpdate();
    }

    public void Clear()
    {
        targets.Clear();
        arrow.gameObject.SetActive(false);
    }

    void LateUpdate()
    {
        Rect bounds = default;
        float top = 0f;
        RectTransform target = targets.Find(t => t != null && t.gameObject.activeInHierarchy);
        bool visible = target != null && TryGetTargetRect(target, out bounds, out top);
        if (arrow.gameObject.activeSelf != visible)
            arrow.gameObject.SetActive(visible);
        if (!visible)
            return;

        float bounce = (Mathf.Sin(Time.unscaledTime * bouncesPerSecond * Mathf.PI * 2f) * 0.5f + 0.5f) * bounceHeight;
        float offset = gap + bounce;
        float room = gap + bounceHeight + arrow.rect.height;
        Rect whole = area.rect;

        if (side == TutorialPointerSide.Auto && top <= pointFromSideAbove)
            Place(new Vector2(bounds.center.x, bounds.yMax + offset), 0f);
        else if (bounds.xMin - whole.xMin >= room || side == TutorialPointerSide.Left && bounds.xMin - whole.xMin >= whole.xMax - bounds.xMax)
            Place(new Vector2(bounds.xMin - offset, bounds.center.y), 90f);
        else
            Place(new Vector2(bounds.xMax + offset, bounds.center.y), -90f);
    }

    void Place(Vector2 position, float angle)
    {
        arrow.anchoredPosition = position;
        arrow.localEulerAngles = new Vector3(0f, 0f, angle);
    }

    /// <summary>対象の画面内に見えている範囲を、この矢印の親の座標で求める。見えていないか、画面のほとんどを覆うなら false</summary>
    bool TryGetTargetRect(RectTransform target, out Rect rect, out float topViewport)
    {
        Canvas targetCanvas = target.GetComponentInParent<Canvas>();
        Camera targetCamera = targetCanvas != null && targetCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? targetCanvas.worldCamera : null;

        var min = new Vector2(float.MaxValue, float.MaxValue);
        var max = new Vector2(float.MinValue, float.MinValue);
        float screenTop = float.MinValue;
        void Include(RectTransform part)
        {
            part.GetWorldCorners(corners);
            foreach (Vector3 corner in corners)
            {
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(targetCamera, corner);
                screenTop = Mathf.Max(screenTop, screen.y);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(area, screen, null, out Vector2 local);
                min = Vector2.Min(min, local);
                max = Vector2.Max(max, local);
            }
        }

        if (target.TryGetComponent(out Graphic _))
        {
            Include(target);
        }
        else
        {
            target.GetComponentsInChildren(false, graphics);
            foreach (Graphic graphic in graphics)
            {
                if (graphic.enabled)
                    Include(graphic.rectTransform);
            }
        }

        Rect whole = area.rect;
        rect = Rect.MinMaxRect(
            Mathf.Max(min.x, whole.xMin), Mathf.Max(min.y, whole.yMin),
            Mathf.Min(max.x, whole.xMax), Mathf.Min(max.y, whole.yMax));
        topViewport = Screen.height > 0 ? Mathf.Min(screenTop / Screen.height, 1f) : 0f;
        if (rect.width <= 0f || rect.height <= 0f)
            return false;
        return rect.width < whole.width * ignoreCoverage || rect.height < whole.height * ignoreCoverage;
    }
}
