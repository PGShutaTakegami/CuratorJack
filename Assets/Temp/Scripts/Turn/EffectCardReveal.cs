using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 効果カードを公開する画面の演出（鑑定・全鑑定・真贋証明・スペシャルカードの発動）。
/// 見出し（誰のカードか）と下地のカード（指定がなければグレーのカード）を並べて表示し、ディゾルブで公開するカードへ同時に変えてから、フェードで消す。
/// 1 人分のカードは横に並べ、入りきらないときはカードを小さくする。
/// </summary>
public class EffectCardReveal : MonoBehaviour
{
    static readonly int ProgressId = Shader.PropertyToID("_Progress");

    [SerializeField] private CanvasGroup group;

    [SerializeField] private TMP_Text titleLabel;

    [Tooltip("カードを並べる場所（中央を基準に横へ並べる）")]
    [SerializeField] private RectTransform row;

    [Tooltip("下地のグレーのカードの画像")]
    [SerializeField] private Sprite baseSprite;

    [Tooltip("ディゾルブのシェーダー（CJ/UIDissolve）を使うマテリアル。実行時は複製して使う")]
    [SerializeField] private Material dissolveMaterial;

    [SerializeField] private Vector2 cardSize = new Vector2(300f, 400f);

    [SerializeField] private float spacing = 30f;

    [Tooltip("カードを並べられる最大の幅。超えるときはカードを小さくする")]
    [SerializeField] private float maxRowWidth = 1700f;

    [SerializeField] private float appearSeconds = 0.25f;

    [Tooltip("下地のカードを出してから、ディゾルブを始めるまでの秒数")]
    [SerializeField] private float baseHoldSeconds = 1f;

    [SerializeField] private float dissolveSeconds = 0.8f;

    [Tooltip("カードが出てから、フェードを始めるまでの秒数")]
    [SerializeField] private float cardHoldSeconds = 1.5f;

    [SerializeField] private float fadeSeconds = 0.4f;

    readonly List<(RectTransform root, Image back, Image front)> slots = new List<(RectTransform, Image, Image)>();
    Material material;

    void Awake()
    {
        material = new Material(dissolveMaterial);
        group.alpha = 0f;
        group.blocksRaycasts = false;
    }

    void OnDestroy()
    {
        if (material != null)
            Destroy(material);
    }

    /// <summary>カードを公開する演出をして、消えるまで待つ。from を渡すと、グレーのカードの代わりにそのカードからディゾルブで変える</summary>
    public IEnumerator Present(string title, IReadOnlyList<CardData> cards, IReadOnlyList<CardData> from = null)
    {
        if (cards == null || cards.Count == 0)
            yield break;

        titleLabel.text = title;
        Layout(cards, from);
        material.SetFloat(ProgressId, 0f);

        yield return Tween(appearSeconds, t => group.alpha = t);
        yield return new WaitForSeconds(baseHoldSeconds);
        yield return Tween(dissolveSeconds, t => material.SetFloat(ProgressId, t));
        yield return new WaitForSeconds(cardHoldSeconds);
        yield return Tween(fadeSeconds, t => group.alpha = 1f - t);
    }

    /// <summary>カードの数だけ下地と公開するカードを中央揃えで並べる（使わない枠は隠す）</summary>
    void Layout(IReadOnlyList<CardData> cards, IReadOnlyList<CardData> from)
    {
        int count = cards.Count;
        while (slots.Count < count)
            slots.Add(CreateSlot(slots.Count));

        float width = Mathf.Min(cardSize.x, (maxRowWidth - spacing * (count - 1)) / count);
        Vector2 size = new Vector2(width, width * cardSize.y / cardSize.x);
        float step = size.x + spacing;
        for (int i = 0; i < slots.Count; i++)
        {
            bool used = i < count;
            slots[i].root.gameObject.SetActive(used);
            if (!used)
                continue;

            slots[i].root.sizeDelta = size;
            slots[i].root.anchoredPosition = new Vector2((i - (count - 1) * 0.5f) * step, 0f);
            slots[i].front.sprite = cards[i] != null ? cards[i].Parameter.CardSprite : null;
            CardData back = from != null && i < from.Count ? from[i] : null;
            slots[i].back.sprite = back != null ? back.Parameter.CardSprite : baseSprite;
        }
    }

    (RectTransform, Image, Image) CreateSlot(int index)
    {
        var root = new GameObject($"Card{index}", typeof(RectTransform), typeof(Image));
        root.layer = gameObject.layer;
        var rootRect = (RectTransform)root.transform;
        rootRect.SetParent(row, false);
        var baseImage = root.GetComponent<Image>();
        baseImage.sprite = baseSprite;
        baseImage.raycastTarget = false;

        var front = new GameObject("Front", typeof(RectTransform), typeof(Image));
        front.layer = gameObject.layer;
        var frontRect = (RectTransform)front.transform;
        frontRect.SetParent(rootRect, false);
        frontRect.anchorMin = Vector2.zero;
        frontRect.anchorMax = Vector2.one;
        frontRect.offsetMin = Vector2.zero;
        frontRect.offsetMax = Vector2.zero;
        var frontImage = front.GetComponent<Image>();
        frontImage.material = material;
        frontImage.raycastTarget = false;
        return (rootRect, baseImage, frontImage);
    }

    static IEnumerator Tween(float seconds, System.Action<float> apply)
    {
        for (float elapsed = 0f; elapsed < seconds; elapsed += Time.deltaTime)
        {
            apply(Mathf.SmoothStep(0f, 1f, elapsed / seconds));
            yield return null;
        }
        apply(1f);
    }
}
