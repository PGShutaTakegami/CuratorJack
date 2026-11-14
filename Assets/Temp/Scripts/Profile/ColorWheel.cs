using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 色相（角度）と彩度（中心からの距離）を選ぶカラーサークル。
/// 円の画像は起動時に作り、押した・なぞった位置にマーカーを動かす。
/// </summary>
[RequireComponent(typeof(RawImage))]
public class ColorWheel : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    [SerializeField] RectTransform marker;
    [SerializeField] int textureSize = 256;

    RawImage wheelImage;
    Texture2D texture;

    public float Hue { get; private set; }
    public float Saturation { get; private set; }

    public event Action Changed;

    void Awake()
    {
        wheelImage = GetComponent<RawImage>();
        texture = CreateWheelTexture(textureSize);
        wheelImage.texture = texture;
        PlaceMarker();
    }

    void OnDestroy()
    {
        if (texture != null)
            Destroy(texture);
    }

    public void OnPointerDown(PointerEventData eventData) => Pick(eventData);

    public void OnDrag(PointerEventData eventData) => Pick(eventData);

    void Pick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;

        var rect = (RectTransform)transform;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position, eventData.pressEventCamera, out Vector2 local))
            return;

        Rect area = rect.rect;
        Vector2 offset = new Vector2(
            (local.x - area.center.x) / (area.width * 0.5f),
            (local.y - area.center.y) / (area.height * 0.5f));
        if (offset.sqrMagnitude > 1f)
            offset.Normalize();

        Hue = Mathf.Repeat(Mathf.Atan2(offset.y, offset.x) / (Mathf.PI * 2f), 1f);
        Saturation = offset.magnitude;
        PlaceMarker();
        Changed?.Invoke();
    }

    void PlaceMarker()
    {
        if (marker == null) return;

        Rect area = ((RectTransform)transform).rect;
        float angle = Hue * Mathf.PI * 2f;
        Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Saturation;
        marker.anchoredPosition = new Vector2(offset.x * area.width * 0.5f, offset.y * area.height * 0.5f);
    }

    static Texture2D CreateWheelTexture(int size)
    {
        var wheel = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        var colors = new Color32[size * size];
        float half = size * 0.5f;
        float edge = 1.5f / half;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - half) / half;
                float dy = (y + 0.5f - half) / half;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float hue = Mathf.Repeat(Mathf.Atan2(dy, dx) / (Mathf.PI * 2f), 1f);

                Color color = Color.HSVToRGB(hue, Mathf.Min(distance, 1f), 1f);
                color.a = Mathf.Clamp01((1f - distance) / edge);
                colors[y * size + x] = color;
            }
        }

        wheel.SetPixels32(colors);
        wheel.Apply();
        return wheel;
    }
}
