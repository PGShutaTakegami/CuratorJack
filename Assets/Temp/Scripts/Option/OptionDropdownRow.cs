using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 今の値をクリック（または決定）すると、選択肢の一覧を開く行。
/// 一覧の項目を選ぶか、一覧の外をクリック・戻る入力で閉じる。閉じている間は左右の入力で前後の選択肢へ移る。
/// 項目が maxVisibleItems を超えると一覧をその高さに抑えてスクロールさせ、キー操作で選んだ項目は見える位置まで送る。
/// 一覧は真下に開き、画面の下に入りきらないときは真上に開く。ほかの UI より手前に出すため、専用の Canvas で描画順を上げる。
/// </summary>
public class OptionDropdownRow : OptionSelectRow, IBackInputHandler
{
    [Tooltip("今の値を表示し、クリックで一覧を開閉するボタン")]
    [SerializeField] private Button openButton;

    [Tooltip("一覧全体（外側クリック用の blocker と項目を含む）。閉じている間は非表示")]
    [SerializeField] private RectTransform list;

    [Tooltip("一覧を手前に描画するための Canvas（Override Sorting）")]
    [SerializeField] private Canvas listCanvas;

    [Tooltip("一覧の外側を覆い、クリックで一覧を閉じる")]
    [SerializeField] private Button blocker;

    [SerializeField] private ScrollRect scrollRect;

    [Tooltip("項目が入りきらないときだけ表示する")]
    [SerializeField] private Scrollbar scrollbar;

    [Tooltip("項目の雛形。選択肢の数だけ複製する")]
    [SerializeField] private OptionDropdownItem itemTemplate;

    [Tooltip("スクロールせずに見せる項目の数")]
    [SerializeField] private int maxVisibleItems = 5;

    [Tooltip("一覧の枠と項目の間の余白")]
    [SerializeField] private float padding = 5f;

    [Tooltip("今の値のボタンと一覧の間の隙間")]
    [SerializeField] private float gap = 2f;

    [Tooltip("真下に開いたとき、画面の下端との間にこれだけ空かなければ真上に開く")]
    [SerializeField] private float screenMargin = 20f;

    readonly List<OptionDropdownItem> items = new List<OptionDropdownItem>();
    int focused;

    public override bool IsExpanded => list.gameObject.activeSelf;

    float ItemHeight => ((RectTransform)itemTemplate.transform).rect.height;

    protected override void Awake()
    {
        base.Awake();
        openButton.onClick.AddListener(Toggle);
        blocker.onClick.AddListener(Close);
        itemTemplate.gameObject.SetActive(false);
    }

    void OnEnable() => Close();

    public override void Setup(IReadOnlyList<string> labels)
    {
        base.Setup(labels);
        foreach (OptionDropdownItem item in items)
            Destroy(item.gameObject);
        items.Clear();

        for (int i = 0; i < labels.Count; i++)
        {
            OptionDropdownItem item = Instantiate(itemTemplate, itemTemplate.transform.parent);
            item.gameObject.SetActive(true);
            item.Setup(labels[i], i);
            item.Clicked += Pick;
            item.Hovered += Focus;
            items.Add(item);
        }

        ResizeList(labels.Count);
    }

    public override void Step(int direction)
    {
        if (!IsExpanded)
            base.Step(direction);
    }

    public override void Submit()
    {
        if (IsExpanded)
            Pick(focused);
        else
            Open();
    }

    public override void Navigate(int direction)
    {
        Focus(Mathf.Clamp(focused + direction, 0, items.Count - 1));
        Reveal(focused);
    }

    public bool HandleBack()
    {
        if (!IsExpanded)
            return false;

        Close();
        return true;
    }

    /// <summary>一覧の高さを見せる項目の数に合わせ、入りきらないときだけスクロールバーの分だけ項目の幅を空ける</summary>
    void ResizeList(int count)
    {
        bool overflow = count > maxVisibleItems;
        int visible = Mathf.Clamp(count, 1, maxVisibleItems);
        list.sizeDelta = new Vector2(list.sizeDelta.x, visible * ItemHeight + padding * 2f);

        scrollbar.gameObject.SetActive(overflow);
        scrollRect.vertical = overflow;
        float right = overflow ? padding * 2f + ((RectTransform)scrollbar.transform).rect.width : padding;
        scrollRect.viewport.offsetMin = new Vector2(padding, padding);
        scrollRect.viewport.offsetMax = new Vector2(-right, -padding);
    }

    void Toggle()
    {
        RequestFocus();
        if (IsExpanded)
            Close();
        else
            Open();
    }

    void Open()
    {
        Canvas rootCanvas = GetComponentInParent<Canvas>().rootCanvas;
        Place(rootCanvas);
        list.gameObject.SetActive(true);
        listCanvas.overrideSorting = true;
        listCanvas.sortingOrder = rootCanvas.sortingOrder + 1;

        LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);
        Focus(Index);
        Reveal(Index);
    }

    /// <summary>今の値のボタンの真下に置き、画面の下に入りきらないなら真上に置き直す</summary>
    void Place(Canvas rootCanvas)
    {
        var button = (RectTransform)openButton.transform;
        float offset = button.rect.height / 2f + gap;
        float x = button.anchoredPosition.x;

        list.pivot = new Vector2(0.5f, 1f);
        list.anchoredPosition = new Vector2(x, button.anchoredPosition.y - offset);

        var corners = new Vector3[4];
        list.GetWorldCorners(corners);
        var root = (RectTransform)rootCanvas.transform;
        if (root.InverseTransformPoint(corners[0]).y < root.rect.yMin + screenMargin)
        {
            list.pivot = new Vector2(0.5f, 0f);
            list.anchoredPosition = new Vector2(x, button.anchoredPosition.y + offset);
        }
    }

    void Close() => list.gameObject.SetActive(false);

    void Pick(int selected)
    {
        Close();
        if (selected != Index)
            Choose(selected);
    }

    void Focus(int index)
    {
        focused = index;
        for (int i = 0; i < items.Count; i++)
            items[i].SetFocused(i == focused);
    }

    /// <summary>index の項目が見えるところまで一覧を送る</summary>
    void Reveal(int index)
    {
        RectTransform content = scrollRect.content;
        float viewHeight = scrollRect.viewport.rect.height;
        float top = index * ItemHeight;
        float bottom = top + ItemHeight;
        float scroll = content.anchoredPosition.y;
        if (top < scroll)
            scroll = top;
        else if (bottom > scroll + viewHeight)
            scroll = bottom - viewHeight;

        float max = Mathf.Max(0f, content.rect.height - viewHeight);
        scrollRect.StopMovement();
        content.anchoredPosition = new Vector2(content.anchoredPosition.x, Mathf.Clamp(scroll, 0f, max));
    }
}
