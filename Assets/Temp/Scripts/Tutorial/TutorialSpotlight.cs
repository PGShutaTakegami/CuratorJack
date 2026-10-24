using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 暗幕より手前に出す UI。対象に並び順を上書きするキャンバスを付け、押せるようにレイキャスターも付ける。
/// 外すときは付けたものを消し、もともとキャンバスがあった対象は並び順を元に戻す。
/// 続けて同じ対象を出すことがあるため、消すのは遅らせずにその場で行う。
/// </summary>
public sealed class TutorialSpotlight
{
    struct Lit
    {
        public Canvas canvas;
        public GraphicRaycaster raycaster;
        public bool addedCanvas;
        public bool previousOverride;
        public int previousOrder;
    }

    readonly List<RectTransform> targets = new List<RectTransform>();
    readonly List<Lit> lit = new List<Lit>();
    int sortingOrder;

    public void Show(int order, IEnumerable<RectTransform> newTargets)
    {
        Clear();
        sortingOrder = order;
        foreach (RectTransform target in newTargets)
        {
            if (target != null)
                targets.Add(target);
        }
        Apply();
    }

    public void Clear()
    {
        Remove();
        targets.Clear();
    }

    /// <summary>一時的に外す（ポーズ画面より手前に出ないように）。Resume で付け直す</summary>
    public void Suspend() => Remove();

    public void Resume() => Apply();

    void Apply()
    {
        if (lit.Count > 0)
            return;

        foreach (RectTransform target in targets)
        {
            if (target == null || IsInsideOtherTarget(target))
                continue;

            var entry = new Lit { canvas = target.GetComponent<Canvas>() };
            entry.addedCanvas = entry.canvas == null;
            if (entry.addedCanvas)
                entry.canvas = target.gameObject.AddComponent<Canvas>();
            entry.previousOverride = entry.canvas.overrideSorting;
            entry.previousOrder = entry.canvas.sortingOrder;
            entry.canvas.overrideSorting = true;
            entry.canvas.sortingOrder = sortingOrder;
            if (target.GetComponent<GraphicRaycaster>() == null)
                entry.raycaster = target.gameObject.AddComponent<GraphicRaycaster>();
            lit.Add(entry);
        }
    }

    /// <summary>ほかの対象の子なら、その対象と一緒に手前に出るので何も付けない</summary>
    bool IsInsideOtherTarget(RectTransform target)
    {
        foreach (RectTransform other in targets)
        {
            if (other != null && other != target && target.IsChildOf(other))
                return true;
        }
        return false;
    }

    void Remove()
    {
        foreach (Lit entry in lit)
        {
            if (entry.raycaster != null)
                Object.DestroyImmediate(entry.raycaster);
            if (entry.canvas == null)
                continue;

            if (entry.addedCanvas)
            {
                Object.DestroyImmediate(entry.canvas);
                continue;
            }
            entry.canvas.overrideSorting = entry.previousOverride;
            entry.canvas.sortingOrder = entry.previousOrder;
        }
        lit.Clear();
    }
}
