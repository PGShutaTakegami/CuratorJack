using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 効果カードを公開する画面の演出（落札時の効果カード・スペシャルカードの発動・虫眼鏡）。
/// 枠は 1 組しかないため、演出中（IsPlaying）に次を始めるときは終わるまで待つ。
/// 見出し（誰のカードか）と下地のカード（指定がなければグレーのカード）を並べて表示し、ディゾルブで公開するカードへ同時に変えてから、フェードで消す。
/// 1 人分のカードは横に並べ、入りきらないときはカードを小さくする。
/// 公開するカードの名前と能力は、ディゾルブに合わせて各カードの上に出す。
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

    [Tooltip("公開するカードの上に重ねる、カード名と能力の見本")]
    [SerializeField] private CardFaceLabels labelsPrefab;

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

    sealed class Slot
    {
        public RectTransform Root;
        public Image Back;
        public Image Front;
        public CardFaceLabels Labels;
    }

    readonly List<Slot> slots = new List<Slot>();
    Material material;

    public bool IsPlaying { get; private set; }

    void Awake()
    {
        material = new Material(dissolveMaterial);
        group.alpha = 0f;
        group.blocksRaycasts = false;
    }

    void OnDisable()
    {
        IsPlaying = false;
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

        IsPlaying = true;
        titleLabel.text = title;
        Layout(cards, from);
        SetProgress(0f);

        yield return Tween(appearSeconds, t => group.alpha = t);
        yield return new WaitForSeconds(baseHoldSeconds);
        SoundEffectPlayer.Play(SoundId.CardDissolve);
        yield return Tween(dissolveSeconds, SetProgress);
        yield return new WaitForSeconds(cardHoldSeconds);
        yield return Tween(fadeSeconds, t => group.alpha = 1f - t);
        IsPlaying = false;
    }

    /// <summary>公開するカードが無いことを、見出しだけで知らせて消えるまで待つ</summary>
    public IEnumerator PresentNone(string title)
    {
        IsPlaying = true;
        titleLabel.text = title;
        Layout(System.Array.Empty<CardData>(), null);

        yield return Tween(appearSeconds, t => group.alpha = t);
        yield return new WaitForSeconds(cardHoldSeconds);
        yield return Tween(fadeSeconds, t => group.alpha = 1f - t);
        IsPlaying = false;
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
            Slot slot = slots[i];
            bool used = i < count;
            slot.Root.gameObject.SetActive(used);
            if (!used)
                continue;

            slot.Root.sizeDelta = size;
            slot.Root.anchoredPosition = new Vector2((i - (count - 1) * 0.5f) * step, 0f);
            slot.Front.sprite = cards[i] != null ? cards[i].Parameter.CardSprite : null;
            slot.Labels.Show(cards[i]);
            CardData back = from != null && i < from.Count ? from[i] : null;
            slot.Back.sprite = back != null ? back.Parameter.CardSprite : baseSprite;
        }
    }

    void SetProgress(float progress)
    {
        material.SetFloat(ProgressId, progress);
        foreach (Slot slot in slots)
            slot.Labels.Alpha = progress;
    }

    Slot CreateSlot(int index)
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

        CardFaceLabels labels = Instantiate(labelsPrefab, rootRect, false);
        var labelsRect = (RectTransform)labels.transform;
        labelsRect.anchorMin = Vector2.zero;
        labelsRect.anchorMax = Vector2.one;
        labelsRect.offsetMin = Vector2.zero;
        labelsRect.offsetMax = Vector2.zero;
        return new Slot { Root = rootRect, Back = baseImage, Front = frontImage, Labels = labels };
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
