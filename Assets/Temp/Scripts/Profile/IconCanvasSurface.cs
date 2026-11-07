using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// お絵かきの面に付けて、押す・なぞる・離すを面の中の位置（左下 0、右上 1）で知らせる。
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class IconCanvasSurface : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    RectTransform rect;
    bool isPressed;

    public event Action<Vector2> StrokeBegan;
    public event Action<Vector2> StrokeMoved;
    public event Action StrokeEnded;

    void Awake()
    {
        rect = (RectTransform)transform;
    }

    void OnDisable()
    {
        if (isPressed)
        {
            isPressed = false;
            StrokeEnded?.Invoke();
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (!TryGetUv(eventData, out Vector2 uv)) return;

        isPressed = true;
        StrokeBegan?.Invoke(uv);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isPressed) return;
        if (TryGetUv(eventData, out Vector2 uv))
            StrokeMoved?.Invoke(uv);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!isPressed || eventData.button != PointerEventData.InputButton.Left) return;

        isPressed = false;
        StrokeEnded?.Invoke();
    }

    bool TryGetUv(PointerEventData eventData, out Vector2 uv)
    {
        uv = default;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position, eventData.pressEventCamera, out Vector2 local))
            return false;

        Rect area = rect.rect;
        uv = new Vector2((local.x - area.xMin) / area.width, (local.y - area.yMin) / area.height);
        return true;
    }
}
