using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// コントローラーのボタンで画面のボタンを押す。
/// 表示されていて押せる状態で、そのボタンの上に何も重なっていない（案内・ポーズ・フェードで塞がれていない）ときだけ押す。
/// </summary>
public static class UIButtonPresser
{
    static readonly List<RaycastResult> hits = new List<RaycastResult>();

    public static bool TryPress(Button button)
    {
        if (button == null || !button.isActiveAndEnabled || !button.IsInteractable() || !IsFrontmost(button))
            return false;

        button.onClick.Invoke();
        return true;
    }

    static bool IsFrontmost(Component target)
    {
        EventSystem eventSystem = EventSystem.current;
        Canvas canvas = target.GetComponentInParent<Canvas>();
        if (eventSystem == null || canvas == null || !(target.transform is RectTransform rect))
            return false;

        Canvas root = canvas.rootCanvas;
        Camera camera = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));

        hits.Clear();
        eventSystem.RaycastAll(new PointerEventData(eventSystem) { position = screen }, hits);
        return hits.Count > 0 && hits[0].gameObject.transform.IsChildOf(target.transform);
    }
}
