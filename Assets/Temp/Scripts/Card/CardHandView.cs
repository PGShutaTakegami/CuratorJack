using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 開かれている手札を表示する。シーンに置いたカードを初期の手札とし、足りない分はプレハブを複製する。
/// 新しいカードほど右・手前に置き、普段は重ねて左へ並べる。カードを引くと画面の右外から右端へスライドし、
/// 既存の手札は左へずれる。カードをクリックすると画面下部の中央で横一列に広げ、カード以外の場所を押すと戻す。
/// </summary>
public class CardHandView : MonoBehaviour, IPointerClickHandler
{
    [Tooltip("複製するカードのプレハブ。最新のカードはプレハブの位置（右端）に置く")]
    [SerializeField] private Image cardPrefab;

    [Tooltip("最初に開いておくカードの枚数")]
    [SerializeField] private int initialCount = 0;

    [Tooltip("重ねているとき、隣のカードに重なる割合（0.5 で半分）")]
    [Range(0f, 1f)]
    [SerializeField] private float overlapRatio = 0.5f;

    [Tooltip("広げたときのカード同士の間隔")]
    [SerializeField] private float spreadSpacing = 20f;

    [Tooltip("広げたときの画面下端・左右端からの余白")]
    [SerializeField] private float spreadMargin = 20f;

    [Tooltip("広げる・重ねるときの移動にかける秒数")]
    [SerializeField] private float moveDuration = 0.2f;

    [Tooltip("カードを 1 枚引くときのスライドにかける秒数")]
    [SerializeField] private float drawDuration = 0.4f;

    [Tooltip("広げたときに元の大きさで並べる最大枚数。これより多いと、この枚数分の幅に収まるようにカードを縮める")]
    [SerializeField] private int fullSizeSpreadCount = 5;

    [Tooltip("シーンに置いてある手札（古い順。後ろほど右・手前）")]
    [SerializeField] private List<Image> cards = new List<Image>();

    readonly List<Vector2> moveFrom = new List<Vector2>();
    readonly List<Vector2> moveTo = new List<Vector2>();
    float scaleFrom = 1f;
    float scaleTo = 1f;
    CardHandLayout stackLayout;
    float currentMoveDuration;
    float moveElapsed;
    bool isMoving;

    public IReadOnlyList<Image> Cards => cards;

    public bool IsSpread { get; private set; }

    public bool IsMoving => isMoving;

    /// <summary>広げているときに、カードがクリックされた</summary>
    public event Action<Image> CardClicked;

    void Awake()
    {
        RectTransform prefabRect = cardPrefab.rectTransform;
        stackLayout = new CardHandLayout(prefabRect.anchoredPosition, prefabRect.sizeDelta.x, overlapRatio);

        cards.RemoveAll(card => card == null);
        while (cards.Count < initialCount)
            CreateCard();

        CardSpreadLayout spreadLayout = CreateSpreadLayout();
        for (int i = 0; i < cards.Count; i++)
        {
            cards[i].rectTransform.anchoredPosition = TargetPosition(i, spreadLayout);
            cards[i].transform.SetAsLastSibling();
        }
    }

    /// <summary>カードを 1 枚引く。画面の右外から右端へスライドさせ、既存の手札は左へずらす。</summary>
    public Image AddCard()
    {
        Image card = CreateCard();
        Vector2 target = TargetPosition(cards.Count - 1, CreateSpreadLayout());
        card.rectTransform.anchoredPosition = new Vector2(OffscreenRightX(), target.y);
        MoveToCurrentLayout(drawDuration);
        return card;
    }

    /// <summary>カードを手札から取り除き、残りを並べ直す。</summary>
    public void RemoveCard(Image card)
    {
        if (!cards.Remove(card))
            return;

        Destroy(card.gameObject);
        MoveToCurrentLayout(moveDuration);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!IsSpread)
        {
            SetSpread(true);
            return;
        }

        Image clicked = FindCard(eventData.pointerCurrentRaycast.gameObject);
        if (clicked != null)
            CardClicked?.Invoke(clicked);
    }

    Image FindCard(GameObject hit)
    {
        for (Transform t = hit != null ? hit.transform : null; t != null && t != transform; t = t.parent)
        {
            if (t.TryGetComponent(out Image image) && cards.Contains(image))
                return image;
        }
        return null;
    }

    public void SetSpread(bool spread)
    {
        IsSpread = spread;
        MoveToCurrentLayout(moveDuration);
    }

    Image CreateCard()
    {
        Image card = Instantiate(cardPrefab, transform);
        card.name = $"{cardPrefab.name}_{cards.Count + 1}";
        card.transform.SetAsLastSibling();
        cards.Add(card);
        return card;
    }

    Vector2 TargetPosition(int index, CardSpreadLayout spreadLayout)
    {
        return IsSpread ? spreadLayout.PositionAt(index) : stackLayout.PositionAt(cards.Count - 1 - index);
    }

    void MoveToCurrentLayout(float duration)
    {
        currentMoveDuration = duration;
        CardSpreadLayout spreadLayout = CreateSpreadLayout();
        moveFrom.Clear();
        moveTo.Clear();
        for (int i = 0; i < cards.Count; i++)
        {
            moveFrom.Add(cards[i].rectTransform.anchoredPosition);
            moveTo.Add(TargetPosition(i, spreadLayout));
        }
        scaleFrom = cards.Count > 0 ? cards[0].rectTransform.localScale.x : 1f;
        scaleTo = TargetScale();

        moveElapsed = 0f;
        isMoving = true;
    }

    /// <summary>広げたときの列の右上（ワールド座標）。広げていなくても、広げたときの位置を返す。</summary>
    public Vector3 SpreadTopRight()
    {
        var self = (RectTransform)transform;
        Vector2 cardSize = cardPrefab.rectTransform.sizeDelta * SpreadScale();
        Vector2 last = CreateSpreadLayout().PositionAt(Mathf.Max(0, cards.Count - 1));
        return self.TransformPoint(self.rect.center + last + cardSize * 0.5f);
    }

    float TargetScale() => IsSpread ? SpreadScale() : 1f;

    /// <summary>広げたときは、fullSizeSpreadCount 枚より多ければその枚数分の幅に収まるように縮める。</summary>
    float SpreadScale()
    {
        int count = cards.Count;
        if (count <= fullSizeSpreadCount)
            return 1f;

        float cardWidth = cardPrefab.rectTransform.sizeDelta.x;
        float fullRow = fullSizeSpreadCount * cardWidth + (fullSizeSpreadCount - 1) * spreadSpacing;
        float row = count * cardWidth + (count - 1) * spreadSpacing;
        return fullRow / row;
    }

    /// <summary>画面サイズは変わりうるため、使うたびに親の矩形から下部中央の位置を求める。</summary>
    CardSpreadLayout CreateSpreadLayout()
    {
        var screen = (RectTransform)transform.parent;
        float scale = SpreadScale();
        Vector2 cardSize = cardPrefab.rectTransform.sizeDelta * scale;
        Rect screenRect = screen.rect;
        Vector2 center = ScreenToLocal(new Vector2(screenRect.center.x, screenRect.yMin + spreadMargin + cardSize.y * 0.5f));

        float maxRowWidth = screenRect.width - spreadMargin * 2f;
        return new CardSpreadLayout(center, cardSize.x, spreadSpacing * scale, maxRowWidth, cards.Count);
    }

    float OffscreenRightX()
    {
        var screen = (RectTransform)transform.parent;
        Rect screenRect = screen.rect;
        float cardWidth = cardPrefab.rectTransform.sizeDelta.x;
        return ScreenToLocal(new Vector2(screenRect.xMax + cardWidth * 0.5f, screenRect.center.y)).x;
    }

    bool IsPressedOutsideCards()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
            return false;

        Vector2 position = mouse.position.ReadValue();
        Canvas canvas = GetComponentInParent<Canvas>();
        Camera eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        foreach (Image card in cards)
        {
            if (RectTransformUtility.RectangleContainsScreenPoint(card.rectTransform, position, eventCamera))
                return false;
        }
        return !IsOverOtherSelectable(position);
    }

    /// <summary>手札以外のボタンなど押せる UI の上か（確定ボタンなどを押しても手札を閉じない）</summary>
    bool IsOverOtherSelectable(Vector2 position)
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
            return false;

        var hits = new List<RaycastResult>();
        eventSystem.RaycastAll(new PointerEventData(eventSystem) { position = position }, hits);
        foreach (RaycastResult hit in hits)
        {
            Selectable selectable = hit.gameObject.GetComponentInParent<Selectable>();
            if (selectable != null && selectable.IsInteractable() && !selectable.transform.IsChildOf(transform))
                return true;
        }
        return false;
    }

    Vector2 ScreenToLocal(Vector2 screenLocalPoint)
    {
        var self = (RectTransform)transform;
        var screen = (RectTransform)transform.parent;
        Vector3 world = screen.TransformPoint(screenLocalPoint);
        return (Vector2)self.InverseTransformPoint(world) - self.rect.center;
    }

    void Update()
    {
        if (IsSpread && IsPressedOutsideCards())
            SetSpread(false);

        if (!isMoving)
            return;

        moveElapsed += Time.unscaledDeltaTime;
        float t = currentMoveDuration > 0f ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(moveElapsed / currentMoveDuration)) : 1f;
        float scale = Mathf.Lerp(scaleFrom, scaleTo, t);
        for (int i = 0; i < cards.Count; i++)
        {
            cards[i].rectTransform.anchoredPosition = Vector2.Lerp(moveFrom[i], moveTo[i], t);
            cards[i].rectTransform.localScale = new Vector3(scale, scale, 1f);
        }

        if (t >= 1f)
            isMoving = false;
    }
}
