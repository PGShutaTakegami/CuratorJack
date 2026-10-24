using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum TutorialNavLayout
{
    /// <summary>画面の中央（スタート前の案内など）</summary>
    Center,

    /// <summary>画面の上側（手札や操作の UI を隠さないように）</summary>
    Top
}

/// <summary>
/// チュートリアルの案内の画面。暗幕・キャラクター・ふきだしを出し、指定した UI だけを暗幕より手前に出す。
/// 手前に出した UI のうち表示されている最初の 1 つを矢印で指す。描く順は 暗幕 → 手前に出した UI → 矢印 → キャラクターとふきだし。
/// 暗幕は透明にしていても入力を止め、チュートリアル中は案内している UI 以外を押せないようにする（Release で外す）。
/// ふきだしのクリックで進む案内では、ふきだしを押せるようにしてクリックの合図を出す。
/// ポーズ画面を開いている間は、案内も入力の制限も止める。
/// </summary>
public class TutorialNavigator : MonoBehaviour
{
    [SerializeField] private Canvas canvas;

    [Tooltip("表示と入力の制限をまとめて止めるのに使う")]
    [SerializeField] private CanvasGroup group;

    [Tooltip("画面全体を覆う暗幕。透明でも入力を止める")]
    [SerializeField] private Image dim;

    [SerializeField] private float dimAlpha = 0.8f;

    [Tooltip("キャラクターとふきだしの親。案内の位置に合わせて動かす")]
    [SerializeField] private RectTransform guide;

    [Tooltip("キャラクターとふきだしを、手前に出した UI より手前に描くための Canvas")]
    [SerializeField] private Canvas guideCanvas;

    [Tooltip("手前に出した UI を指す矢印")]
    [SerializeField] private TutorialPointer pointer;

    [Tooltip("矢印を、手前に出した UI より手前（キャラクターとふきだしより奥）に描くための Canvas")]
    [SerializeField] private Canvas pointerCanvas;

    [SerializeField] private TMP_Text message;

    [Tooltip("ふきだしのクリックで進む案内のときだけ押せるようにする")]
    [SerializeField] private Button bubbleButton;

    [Tooltip("ふきだしのクリックで進む案内のときだけ出す合図")]
    [SerializeField] private GameObject clickHint;

    [SerializeField] private LanguageTable table;

    [SerializeField] private Vector2 centerPosition = Vector2.zero;

    [SerializeField] private float centerScale = 1f;

    [SerializeField] private Vector2 topPosition = new Vector2(0f, 290f);

    [SerializeField] private float topScale = 0.8f;

    readonly TutorialSpotlight spotlight = new TutorialSpotlight();
    string messageKey;
    bool blocksInput = true;
    bool suspended;

    /// <summary>ふきだしのクリックで進む案内で、ふきだしが押された</summary>
    public event Action BubbleClicked;

    int HighlightOrder => canvas.sortingOrder + 1;

    void Awake()
    {
        pointerCanvas.overrideSorting = true;
        pointerCanvas.sortingOrder = HighlightOrder + 1;
        guideCanvas.overrideSorting = true;
        guideCanvas.sortingOrder = HighlightOrder + 2;
        bubbleButton.onClick.AddListener(() => BubbleClicked?.Invoke());
    }

    void OnEnable()
    {
        GameLanguage.Changed += RefreshMessage;
    }

    void OnDisable()
    {
        GameLanguage.Changed -= RefreshMessage;
    }

    void OnDestroy()
    {
        spotlight.Clear();
    }

    /// <summary>案内を出す。dimmed なら highlights 以外を暗くし、waitsClick ならふきだしを押せるようにする</summary>
    public void Show(string key, TutorialNavLayout layout, bool dimmed, bool waitsClick, params RectTransform[] highlights)
    {
        Show(key, layout, dimmed, waitsClick, TutorialPointerSide.Auto, highlights);
    }

    /// <summary>案内を出す。矢印は highlights のうち表示されている最初の 1 つを、pointerSide の向きから指す</summary>
    public void Show(string key, TutorialNavLayout layout, bool dimmed, bool waitsClick, TutorialPointerSide pointerSide, params RectTransform[] highlights)
    {
        messageKey = key;
        RefreshMessage();

        bool isTop = layout == TutorialNavLayout.Top;
        guide.anchoredPosition = isTop ? topPosition : centerPosition;
        float scale = isTop ? topScale : centerScale;
        guide.localScale = new Vector3(scale, scale, 1f);
        guide.gameObject.SetActive(true);

        SetDimAlpha(dimmed ? dimAlpha : 0f);
        bubbleButton.interactable = waitsClick;
        clickHint.SetActive(waitsClick);

        spotlight.Show(HighlightOrder, highlights);
        if (suspended)
            spotlight.Suspend();
        pointer.PointAt(pointerSide, highlights);

        blocksInput = true;
        ApplyGroup();
    }

    /// <summary>案内を消す。入力の制限は残す</summary>
    public void Hide()
    {
        guide.gameObject.SetActive(false);
        SetDimAlpha(0f);
        spotlight.Clear();
        pointer.Clear();
    }

    /// <summary>案内を消し、入力の制限も外す。次の Show でまた制限する</summary>
    public void Release()
    {
        Hide();
        blocksInput = false;
        ApplyGroup();
    }

    /// <summary>チュートリアルを終える。案内を消し、入力の制限も外す</summary>
    public void Finish()
    {
        Release();
        canvas.gameObject.SetActive(false);
    }

    /// <summary>ポーズ画面を開いている間は、案内も入力の制限も止める</summary>
    public void SetSuspended(bool value)
    {
        if (suspended == value)
            return;

        suspended = value;
        ApplyGroup();
        if (value)
            spotlight.Suspend();
        else
            spotlight.Resume();
    }

    void ApplyGroup()
    {
        group.alpha = suspended ? 0f : 1f;
        group.blocksRaycasts = blocksInput && !suspended;
    }

    void SetDimAlpha(float alpha)
    {
        Color color = dim.color;
        color.a = alpha;
        dim.color = color;
    }

    void RefreshMessage()
    {
        if (!string.IsNullOrEmpty(messageKey))
            message.text = table.Get(messageKey);
    }
}
