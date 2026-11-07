using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// ほかのプレイヤーの横（テーブルの高さで、体に重ならない画面の中央側）に虫眼鏡のボタンを出し、
/// 押すとその人の置き場を TableInspectCamera で見せる。
/// 出すのは出品ターンとオークションの間だけ。見ている間、自分のカメラが演出で動いている間、ボタンが画面の外に出る間も隠す。
/// </summary>
public class TableInspectButtons : MonoBehaviour
{
    [SerializeField] private PlayerRegistry registry;

    [Tooltip("出品ターンとオークションの始まり・終わりを知らせる進行")]
    [SerializeField] private TurnFlowController turnFlow;

    [SerializeField] private TableInspectCamera inspectCamera;

    [Tooltip("ボタンの位置の基準にする、その人の置き場")]
    [SerializeField] private PlayerCardPiles piles;

    [Tooltip("ボタンを置く画面の UI（画面に直接重ねるキャンバス）。ほかの UI より奥に置く")]
    [SerializeField] private RectTransform screen;

    [Tooltip("ボタンの丸い背景")]
    [SerializeField] private Sprite backgroundSprite;

    [Tooltip("虫眼鏡の記号（白。iconColor を掛ける）")]
    [SerializeField] private Sprite iconSprite;

    [Tooltip("ボタンの直径（キャンバスの単位）")]
    [SerializeField] private float buttonSize = 64f;

    [Tooltip("ボタンの直径に対する、記号の大きさ")]
    [Range(0f, 1f)]
    [SerializeField] private float iconRatio = 0.6f;

    [SerializeField] private Color backgroundColor = new Color(0.12f, 0.1f, 0.08f, 0.9f);

    [SerializeField] private Color outlineColor = new Color(0.85f, 0.68f, 0.35f, 1f);

    [SerializeField] private Color iconColor = new Color(0.95f, 0.82f, 0.55f, 1f);

    [Tooltip("置き場から、その人の席の側へずらす距離")]
    [SerializeField] private float seatOffset = 0.3f;

    [Tooltip("置き場から、画面の中央の側（正面の人は右）へずらす距離。体に重ならない長さ")]
    [SerializeField] private float sideOffset = 1f;

    [Tooltip("テーブルの面から上げる高さ")]
    [SerializeField] private float lift = 0.1f;

    [Tooltip("左右のどちらへずらしても画面の中央からの距離の差がこの割合（画面の幅に対する）未満なら、右へずらす")]
    [Range(0f, 1f)]
    [SerializeField] private float frontToleranceRatio = 0.02f;

    readonly Dictionary<int, RectTransform> markers = new Dictionary<int, RectTransform>();
    RectTransform container;
    bool isListing;
    bool isAuction;

    void OnEnable()
    {
        turnFlow.ListingStarted += HandleListingStarted;
        turnFlow.ListingFinished += HandleListingFinished;
        turnFlow.AuctionStarted += HandleAuctionStarted;
        turnFlow.AuctionFinished += HandleAuctionFinished;
    }

    void OnDisable()
    {
        turnFlow.ListingStarted -= HandleListingStarted;
        turnFlow.ListingFinished -= HandleListingFinished;
        turnFlow.AuctionStarted -= HandleAuctionStarted;
        turnFlow.AuctionFinished -= HandleAuctionFinished;
        isListing = false;
        isAuction = false;
    }

    void HandleListingStarted(ListingSession session) => isListing = true;

    void HandleListingFinished() => isListing = false;

    void HandleAuctionStarted(AuctionModel auction) => isAuction = true;

    void HandleAuctionFinished() => isAuction = false;

    void LateUpdate()
    {
        int local = registry.LocalPlayerNumber;
        foreach (int number in registry.PlayerNumbers)
        {
            if (number != local && !markers.ContainsKey(number))
                markers[number] = CreateMarker(number);
        }

        bool shows = (isListing || isAuction) && inspectCamera.CanInspect;
        Camera viewer = shows ? inspectCamera.LocalCamera : null;
        foreach (KeyValuePair<int, RectTransform> pair in markers)
            Show(pair.Value, pair.Key, pair.Key == local ? null : viewer);
    }

    void Show(RectTransform marker, int playerNumber, Camera viewer)
    {
        Vector2 point = Vector2.zero;
        bool visible = viewer != null && TryGetScreenPoint(playerNumber, viewer, out point);
        if (marker.gameObject.activeSelf != visible)
            marker.gameObject.SetActive(visible);
        if (visible)
            marker.position = point;
    }

    /// <summary>置き場から席の側へ寄せた点を、左右のうち画面の中央に近い側へずらして画面に映した位置</summary>
    bool TryGetScreenPoint(int playerNumber, Camera viewer, out Vector2 point)
    {
        point = Vector2.zero;
        if (!piles.TryGetSpot(playerNumber, out Pose spot))
            return false;

        Vector3 basePoint = spot.position - spot.rotation * Vector3.forward * seatOffset + Vector3.up * lift;
        Vector3 lateral = spot.rotation * Vector3.right * sideOffset;
        Vector3 right = viewer.WorldToScreenPoint(basePoint + lateral);
        Vector3 left = viewer.WorldToScreenPoint(basePoint - lateral);

        float center = viewer.pixelRect.center.x;
        float rightGap = Mathf.Abs(right.x - center);
        float leftGap = Mathf.Abs(left.x - center);
        Vector3 chosen;
        if (Mathf.Abs(rightGap - leftGap) < viewer.pixelWidth * frontToleranceRatio)
            chosen = right.x > left.x ? right : left;
        else
            chosen = rightGap < leftGap ? right : left;

        point = chosen;
        return chosen.z > 0f && viewer.pixelRect.Contains(point);
    }

    RectTransform CreateMarker(int playerNumber)
    {
        var go = new GameObject($"InspectButton_Player{playerNumber}", typeof(RectTransform), typeof(Image), typeof(Outline), typeof(Button));
        var rect = (RectTransform)go.transform;
        rect.SetParent(Container(), false);
        rect.sizeDelta = new Vector2(buttonSize, buttonSize);

        var background = go.GetComponent<Image>();
        background.sprite = backgroundSprite;
        background.color = backgroundColor;

        var outline = go.GetComponent<Outline>();
        outline.effectColor = outlineColor;
        outline.effectDistance = new Vector2(2f, -2f);

        var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        var iconRect = (RectTransform)icon.transform;
        iconRect.SetParent(rect, false);
        iconRect.sizeDelta = new Vector2(buttonSize * iconRatio, buttonSize * iconRatio);
        var iconImage = icon.GetComponent<Image>();
        iconImage.sprite = iconSprite;
        iconImage.color = iconColor;
        iconImage.raycastTarget = false;

        var button = go.GetComponent<Button>();
        button.targetGraphic = background;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.AddListener(() =>
        {
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
            inspectCamera.Inspect(playerNumber);
        });

        go.SetActive(false);
        return rect;
    }

    RectTransform Container()
    {
        if (container != null)
            return container;

        container = new GameObject("TableInspectButtons", typeof(RectTransform)).GetComponent<RectTransform>();
        container.SetParent(screen, false);
        container.SetAsFirstSibling();
        container.anchorMin = Vector2.zero;
        container.anchorMax = Vector2.one;
        container.offsetMin = Vector2.zero;
        container.offsetMax = Vector2.zero;
        return container;
    }
}
