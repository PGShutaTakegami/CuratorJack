using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 渡された画面上の範囲（画面の端や、映っている人の体の周り）の縁に、稲妻を短い間隔で置き直してバチバチと走らせる。
/// 縁は四角（画面の端向け）か、範囲に収まる楕円（体の周り向け）のどちらかに沿わせる。
/// 画面に直接重ねるキャンバスの下に置き、操作はふさがない。
/// </summary>
public class ElectricBolts : MonoBehaviour
{
    const int ShapeCount = 6;

    [SerializeField] private Color color = new Color(0.65f, 0.9f, 1f, 1f);

    [Tooltip("範囲の四角の縁ではなく、範囲に収まる楕円に沿って走らせる")]
    [SerializeField] private bool elliptical;

    [Tooltip("1 つの範囲に同時に走らせる稲妻の数")]
    [SerializeField] private int boltsPerArea = 5;

    [Tooltip("範囲の短い辺に対する、稲妻の長さ")]
    [SerializeField] private float lengthRatio = 0.35f;

    [Tooltip("稲妻の長さの上限（キャンバスの単位。0 なら上限なし）")]
    [SerializeField] private float maxLength;

    [Tooltip("稲妻の長さに対する太さ")]
    [SerializeField] private float thicknessRatio = 0.25f;

    [Tooltip("稲妻を置き直す間隔（秒）")]
    [SerializeField] private float interval = 0.06f;

    [Tooltip("置き直すたびに、1 本ずつ消しておく確率（ちらつき）")]
    [Range(0f, 1f)]
    [SerializeField] private float hideChance = 0.3f;

    readonly List<Rect> areas = new List<Rect>();
    readonly List<Image> pool = new List<Image>();
    Sprite[] shapes;
    Canvas canvas;
    float timer;

    void Awake()
    {
        canvas = GetComponentInParent<Canvas>();
        shapes = new Sprite[ShapeCount];
        for (int i = 0; i < ShapeCount; i++)
            shapes[i] = ScreenEffectSprites.CreateBolt(i + 1);
    }

    /// <summary>稲妻を走らせる画面上の範囲（画面のピクセル）。空なら消す</summary>
    public void SetAreas(IReadOnlyList<Rect> screenAreas)
    {
        areas.Clear();
        areas.AddRange(screenAreas);
        if (areas.Count == 0)
            HideFrom(0);
    }

    void Update()
    {
        if (areas.Count == 0)
            return;

        timer -= Time.unscaledDeltaTime;
        if (timer > 0f)
            return;

        timer = interval;
        int used = 0;
        foreach (Rect area in areas)
        {
            for (int i = 0; i < boltsPerArea; i++)
                Place(BoltAt(used++), area);
        }
        HideFrom(used);
    }

    void Place(Image bolt, Rect area)
    {
        bool visible = Random.value >= hideChance;
        bolt.gameObject.SetActive(visible);
        if (!visible)
            return;

        float scale = canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
        float length = Mathf.Min(area.width, area.height) * lengthRatio * Random.Range(0.6f, 1.2f) / scale;
        if (maxLength > 0f)
            length = Mathf.Min(length, maxLength * Random.Range(0.6f, 1f));
        var rect = bolt.rectTransform;
        rect.sizeDelta = new Vector2(length, length * thicknessRatio);
        float tangentAngle;
        rect.position = elliptical ? PointOnEllipse(area, out tangentAngle) : PointOnBorder(area, out tangentAngle);
        rect.rotation = Quaternion.Euler(0f, 0f, tangentAngle + Random.Range(-35f, 35f));
        rect.localScale = new Vector3(1f, Random.value < 0.5f ? 1f : -1f, 1f);
        bolt.sprite = shapes[Random.Range(0, shapes.Length)];
        bolt.color = new Color(color.r, color.g, color.b, color.a * Random.Range(0.7f, 1f));
    }

    /// <summary>範囲に収まる楕円上のランダムな点と、その点での接線の向き（度）</summary>
    static Vector2 PointOnEllipse(Rect area, out float tangentAngle)
    {
        float angle = Random.value * 360f;
        float radians = angle * Mathf.Deg2Rad;
        tangentAngle = angle + 90f;
        return area.center + new Vector2(Mathf.Cos(radians) * area.width, Mathf.Sin(radians) * area.height) * 0.5f;
    }

    /// <summary>範囲の縁のランダムな点と、その辺の向き（度）</summary>
    static Vector2 PointOnBorder(Rect area, out float tangentAngle)
    {
        float perimeter = 2f * (area.width + area.height);
        float distance = Random.value * perimeter;
        if (distance < area.width)
        {
            tangentAngle = 0f;
            return new Vector2(area.xMin + distance, area.yMin);
        }
        distance -= area.width;
        if (distance < area.height)
        {
            tangentAngle = 90f;
            return new Vector2(area.xMax, area.yMin + distance);
        }
        distance -= area.height;
        if (distance < area.width)
        {
            tangentAngle = 0f;
            return new Vector2(area.xMax - distance, area.yMax);
        }
        distance -= area.width;
        tangentAngle = 90f;
        return new Vector2(area.xMin, area.yMax - distance);
    }

    Image BoltAt(int index)
    {
        while (pool.Count <= index)
        {
            var go = new GameObject($"Bolt{pool.Count}", typeof(RectTransform), typeof(Image));
            go.layer = gameObject.layer;
            go.transform.SetParent(transform, false);
            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            pool.Add(image);
        }
        return pool[index];
    }

    void HideFrom(int index)
    {
        for (int i = index; i < pool.Count; i++)
            pool[i].gameObject.SetActive(false);
    }
}
