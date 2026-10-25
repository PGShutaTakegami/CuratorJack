using System;
using TMPro;
using UnityEngine;

/// <summary>
/// 出品・オークションの待ち時間を数えている間（ルーム設定で時間制限があるとき）だけ、円形の枠に残り秒数を表示する。
/// 表示するのは自分が対象者のときだけ（出品では自分が出品者のとき、オークションでは自分が降りていない参加者のとき）。
/// オークション中は画面右下に置き、演出（他の人の入札のアップ）の間はフェードで隠し、数え始めたらフェードで出す。
/// 出品中は手札や確定ボタンに重ならない画面右上（ポーズボタンの下）に置き、フェードせずに出し入れする。時間制限が無制限なら表示しない。
/// </summary>
public class PhaseTimerView : MonoBehaviour
{
    [Serializable]
    struct Placement
    {
        [Tooltip("基準にする画面の位置（0〜1。右下は (1, 0)）。枠もこの位置を基準に置く")]
        public Vector2 anchor;

        [Tooltip("基準の位置からのずれ")]
        public Vector2 position;
    }

    [SerializeField] private PhaseTimer timer;

    [SerializeField] private TurnFlowController turnFlow;

    [SerializeField] private PlayerRegistry registry;

    [Tooltip("残り秒数を囲む円形の枠。表示しない間は隠す")]
    [SerializeField] private RectTransform frame;

    [Tooltip("枠ごとフェードさせる")]
    [SerializeField] private CanvasGroup frameGroup;

    [SerializeField] private TMP_Text label;

    [Tooltip("オークション中に出し入れするフェードの秒数")]
    [SerializeField] private float fadeSeconds = 0.3f;

    [SerializeField] private Placement auctionPlacement = new Placement { anchor = new Vector2(1f, 0f), position = new Vector2(-60f, 70f) };

    [SerializeField] private Placement listingPlacement = new Placement { anchor = new Vector2(1f, 1f), position = new Vector2(-60f, -150f) };

    int shownSeconds = -1;
    bool fades;

    void Awake()
    {
        turnFlow.ListingStarted += HandleListingStarted;
        turnFlow.AuctionStarted += HandleAuctionStarted;
        frameGroup.alpha = 0f;
        frame.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        turnFlow.ListingStarted -= HandleListingStarted;
        turnFlow.AuctionStarted -= HandleAuctionStarted;
    }

    void HandleListingStarted(ListingSession session) => Place(listingPlacement, false);

    void HandleAuctionStarted(AuctionModel auction) => Place(auctionPlacement, true);

    void Place(Placement placement, bool fadesInOut)
    {
        fades = fadesInOut;
        frameGroup.alpha = 0f;
        frame.anchorMin = placement.anchor;
        frame.anchorMax = placement.anchor;
        frame.pivot = placement.anchor;
        frame.anchoredPosition = placement.position;
    }

    void Update()
    {
        bool visible = timer.IsRunning && timer.IsTarget(registry.LocalPlayerNumber);
        if (visible)
            ShowSeconds(Mathf.CeilToInt(timer.RemainingSeconds));

        float target = visible ? 1f : 0f;
        frameGroup.alpha = fades && fadeSeconds > 0f
            ? Mathf.MoveTowards(frameGroup.alpha, target, Time.unscaledDeltaTime / fadeSeconds)
            : target;

        bool active = frameGroup.alpha > 0f;
        if (frame.gameObject.activeSelf != active)
            frame.gameObject.SetActive(active);
        if (!active)
            shownSeconds = -1;
    }

    void ShowSeconds(int seconds)
    {
        if (seconds == shownSeconds)
            return;

        shownSeconds = seconds;
        label.text = seconds.ToString();
    }
}
