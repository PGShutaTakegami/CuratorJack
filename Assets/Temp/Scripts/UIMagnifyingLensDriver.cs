using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public sealed class UIMagnifyingLensDriver : MonoBehaviour
{
    static readonly int CenterRadiusId = Shader.PropertyToID("_MagnifyCenterRadius");
    static readonly int ZoomId = Shader.PropertyToID("_MagnifyZoom");

    [SerializeField, Min(1f)] float zoom = 4f;

    Canvas canvas;
    RectTransform rectTransform;
    readonly Vector3[] corners = new Vector3[4];
    bool subscribed;

    void OnEnable()
    {
        Cache();
        Subscribe();
    }

    void OnDisable() => Unsubscribe();

    void OnValidate()
    {
        Cache();
        zoom = Mathf.Max(1f, zoom);
    }

    void Cache()
    {
        rectTransform = transform as RectTransform;
        canvas = GetComponentInParent<Canvas>();
    }

    void Subscribe()
    {
        if (subscribed) return;
        Canvas.willRenderCanvases += PushGlobals;
        subscribed = true;
    }

    void Unsubscribe()
    {
        if (!subscribed) return;
        Canvas.willRenderCanvases -= PushGlobals;
        subscribed = false;
    }

    void LateUpdate()
    {
        if (!Application.isPlaying)
            PushGlobals();
    }

    void PushGlobals()
    {
        if (rectTransform == null) Cache();
        if (rectTransform == null) return;
        if (canvas == null) canvas = GetComponentInParent<Canvas>();

        Camera cam = null;
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            cam = canvas.worldCamera != null ? canvas.worldCamera : Camera.main;

        rectTransform.GetWorldCorners(corners);

        float w, h, ox, oy;
        if (cam != null)
        {
            Rect pr = cam.pixelRect;
            w = Mathf.Max(pr.width, 1f);
            h = Mathf.Max(pr.height, 1f);
            ox = pr.x;
            oy = pr.y;
        }
        else
        {
            w = Mathf.Max(Screen.width, 1);
            h = Mathf.Max(Screen.height, 1);
            ox = 0f;
            oy = 0f;
        }

        float aspect = w / Mathf.Max(h, 1f);

        Vector2 ToUV(Vector3 world)
        {
            Vector2 s = RectTransformUtility.WorldToScreenPoint(cam, world);
            return new Vector2((s.x - ox) / w, (s.y - oy) / h);
        }

        Vector2 u0 = ToUV(corners[0]);
        Vector2 u1 = ToUV(corners[1]);
        Vector2 u2 = ToUV(corners[2]);
        Vector2 u3 = ToUV(corners[3]);

        float minX = Mathf.Min(Mathf.Min(u0.x, u1.x), Mathf.Min(u2.x, u3.x));
        float maxX = Mathf.Max(Mathf.Max(u0.x, u1.x), Mathf.Max(u2.x, u3.x));
        float minY = Mathf.Min(Mathf.Min(u0.y, u1.y), Mathf.Min(u2.y, u3.y));
        float maxY = Mathf.Max(Mathf.Max(u0.y, u1.y), Mathf.Max(u2.y, u3.y));

        Vector2 center = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);

        // Inradius in aspect-corrected UV → circle inscribed in the Image rect (not circumcircle).
        float halfW = (maxX - minX) * 0.5f * aspect;
        float halfH = (maxY - minY) * 0.5f;
        float radius = Mathf.Min(halfW, halfH);

        Shader.SetGlobalVector(CenterRadiusId, new Vector4(center.x, center.y, radius, aspect));
        Shader.SetGlobalFloat(ZoomId, zoom);
    }
}
