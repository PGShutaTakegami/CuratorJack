using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ターン中に使う画面の UI（状態表示・案内・進行状況・出品の確定ボタン・オークションの金額と入札/降りる・勝利表示とリトライ/タイトル）。
/// 表示と押された通知だけを受け持ち、判断は操作役（LocalPlayerAgent）が行う。
/// </summary>
public class TurnUI : MonoBehaviour
{
    [SerializeField] private TMP_Text statusText;

    [SerializeField] private TMP_Text promptText;

    [Tooltip("ターンの進行状況（誰のターンか・出品物・オークションの状況・結果）。操作できない人も含め全員に表示する")]
    [SerializeField] private TMP_Text turnInfoText;

    [Tooltip("出品できる間だけ表示する丸いボタン。押せないときは灰色、押せるときは橙色にする")]
    [SerializeField] private Button confirmButton;

    [Tooltip("確定ボタンのフェードに使う。手札を広げている間だけ表示する")]
    [SerializeField] private CanvasGroup confirmGroup;

    [SerializeField] private float confirmFadeSeconds = 0.25f;

    [Tooltip("確定ボタンの画面右端からの余白と、手札を広げたときの列の上端からの高さ")]
    [SerializeField] private Vector2 confirmOffset = new Vector2(-20f, 20f);

    [Tooltip("確定ボタンと増額の選択の間隔")]
    [SerializeField] private float raiseGap = 20f;

    [SerializeField] private Color confirmDisabledColor = new Color(0.5f, 0.5f, 0.5f);

    [SerializeField] private Color confirmEnabledColor = new Color(1f, 0.55f, 0f);

    [Tooltip("バフデバフカードを付けたときに出す、最低出品額を増額するかの選択")]
    [SerializeField] private GameObject raisePanel;

    [Tooltip("クリックで 0 と +5 を巻いて切り替えるリール")]
    [SerializeField] private ReelToggleView raiseReel;

    [SerializeField] private GameObject auctionPanel;

    [Tooltip("入札 UI のフェードに使う。演出中は消して操作も受け付けない")]
    [SerializeField] private CanvasGroup auctionGroup;

    [SerializeField] private float auctionFadeSeconds = 0.3f;

    [Tooltip("入札する金額。リールの枠を桁の数だけ横に並べ、増えたら上から下へ、減ったら下から上へ巻く")]
    [SerializeField] private ReelNumberView amountReels;

    [SerializeField] private TMP_Text auctionInfoText;

    [Tooltip("オークションで今の金額を入札する。押せないときは灰色、押せるときは橙色にする")]
    [SerializeField] private Button bidButton;

    [SerializeField] private Button foldButton;

    [SerializeField] private GameObject victoryPanel;

    [SerializeField] private TMP_Text victoryText;

    [Tooltip("リザルトから同じ顔ぶれでもう一度遊ぶ")]
    [SerializeField] private Button retryButton;

    [Tooltip("リザルトからタイトルへ戻る")]
    [SerializeField] private Button titleButton;

    public event Action ConfirmClicked;

    public event Action RetryClicked;

    public event Action TitleClicked;

    /// <summary>増額の選択が押された（YES なら true）</summary>
    public event Action<bool> RaiseClicked;

    public event Action FoldClicked;

    public event Action BidClicked;

    void Awake()
    {
        confirmButton.onClick.AddListener(() => ConfirmClicked?.Invoke());
        bidButton.onClick.AddListener(() => BidClicked?.Invoke());
        foldButton.onClick.AddListener(() => FoldClicked?.Invoke());
        retryButton.onClick.AddListener(() => RetryClicked?.Invoke());
        titleButton.onClick.AddListener(() => TitleClicked?.Invoke());
        raiseReel.Clicked += () => RaiseClicked?.Invoke(!isRaised);

        ShowConfirm(false, false);
        SetBidInteractable(false);
        ShowRaise(false, false);
        ShowAuction(false);
        victoryPanel.SetActive(false);
        SetPrompt(string.Empty);
        SetTurnInfo(string.Empty);
    }

    public void SetTurnInfo(string text) => turnInfoText.text = text;

    public void SetStatus(string text) => statusText.text = text;

    public void SetPrompt(string text) => promptText.text = text;

    bool confirmRevealed;
    bool auctionHidden;

    /// <summary>入札 UI を演出のために消している間（フェード中も含む）。この間は金額の操作も受け付けない</summary>
    public bool IsAuctionHidden => auctionHidden || auctionGroup.alpha < 1f;

    void Update()
    {
        if (auctionPanel.activeSelf)
            auctionGroup.alpha = Fade(auctionGroup.alpha, auctionHidden ? 0f : 1f, auctionFadeSeconds);

        if (confirmButton.gameObject.activeSelf)
            confirmGroup.alpha = Fade(confirmGroup.alpha, confirmRevealed ? 1f : 0f, confirmFadeSeconds);
    }

    static float Fade(float alpha, float target, float seconds)
    {
        return Mathf.MoveTowards(alpha, target, seconds > 0f ? Time.deltaTime / seconds : 1f);
    }

    /// <summary>入札 UI をフェードで消す / 出す（消している間は押せない）</summary>
    public void HideAuction(bool hidden)
    {
        auctionHidden = hidden;
        auctionGroup.interactable = !hidden;
        auctionGroup.blocksRaycasts = !hidden;
    }

    /// <summary>出品できる間だけ確定ボタンを有効にする。表示されるのは RevealConfirm で手札を広げている間だけ</summary>
    public void ShowConfirm(bool visible, bool interactable)
    {
        if (!visible)
        {
            confirmRevealed = false;
            confirmGroup.alpha = 0f;
            confirmGroup.blocksRaycasts = false;
        }
        confirmButton.gameObject.SetActive(visible);
        SetButtonState(confirmButton, interactable);
    }

    /// <summary>手札を広げたらフェードで表示し、閉じたらフェードで消す（消えている間は押せない）</summary>
    public void RevealConfirm(bool revealed)
    {
        confirmRevealed = revealed;
        confirmGroup.blocksRaycasts = revealed;
    }

    /// <summary>確定ボタンを画面の右端・手札の列の上端（ワールド座標）の高さに置き、増額の選択をその左に並べる</summary>
    public void PlaceConfirm(Vector3 handTopRight)
    {
        var confirmRect = (RectTransform)confirmButton.transform;
        confirmRect.anchorMin = new Vector2(1f, confirmRect.anchorMin.y);
        confirmRect.anchorMax = new Vector2(1f, confirmRect.anchorMax.y);
        confirmRect.pivot = new Vector2(1f, 0f);
        confirmRect.position = handTopRight;
        confirmRect.anchoredPosition = new Vector2(confirmOffset.x, confirmRect.anchoredPosition.y + confirmOffset.y);

        var raiseRect = (RectTransform)raisePanel.transform;
        raiseRect.pivot = new Vector2(1f, 0.5f);
        raiseRect.position = confirmRect.TransformPoint(new Vector2(confirmRect.rect.xMin, confirmRect.rect.center.y));
        raiseRect.anchoredPosition += new Vector2(-raiseGap, 0f);
    }

    public void SetBidInteractable(bool interactable) => SetButtonState(bidButton, interactable);

    void SetButtonState(Button button, bool interactable)
    {
        button.interactable = interactable;
        button.image.color = interactable ? confirmEnabledColor : confirmDisabledColor;
    }

    bool isRaised;

    /// <summary>増額の選択を表示する。表示中に値が変わったときだけリールを巻いて切り替える</summary>
    public void ShowRaise(bool visible, bool raised)
    {
        bool wasVisible = raisePanel.activeSelf;
        raisePanel.SetActive(visible);
        raiseReel.SetValue(raised, wasVisible && visible);
        isRaised = raised;
    }

    bool amountShown;

    /// <summary>入札 UI を開く / 閉じる。演出で消している最中なら消えたまま開き、演出の後にフェードで出る</summary>
    public void ShowAuction(bool visible)
    {
        auctionPanel.SetActive(visible);
        auctionGroup.alpha = auctionHidden ? 0f : 1f;
        amountShown = false;
    }

    /// <summary>金額は、オークションを開いて最初の表示だけ巻かずに出し、その後は変わるたびに巻く</summary>
    public void SetAuction(int amount, string info)
    {
        amountReels.SetValue(amount, amountShown);
        amountShown = true;
        auctionInfoText.text = info;
    }

    /// <summary>ゲーム終了の表示。見出し・勝者・全員の結果を大きい順に並べる</summary>
    public void ShowVictory(string title, string winner, string results)
    {
        victoryText.text = $"<size=140>{title}</size>\n<size=70>{winner}</size>\n\n<size=40>{results}</size>";
        SetResultButtonsInteractable(true);
        victoryPanel.SetActive(true);
    }

    /// <summary>リトライ / タイトルを押したあと、遷移が終わるまで二重に押せないようにする</summary>
    public void SetResultButtonsInteractable(bool interactable)
    {
        retryButton.interactable = interactable;
        titleButton.interactable = interactable;
    }
}
