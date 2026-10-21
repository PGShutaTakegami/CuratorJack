using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// 出品中の商品を、テーブル中央のマットに表示する。カードのオブジェクトをマットに置いて残し、
/// カメラで真上から見せてから、最低出品額をカードの手前（自分の席の側）に、自分から読める向きで
/// テーブルに寝かせてフェードで表示し、カメラが席へ戻るのと同時に席の方へ起こす（出品の間だけ）。
/// 最低出品額は数字のみで、増額・値引きしていれば「初期価格+増額-値引き額」と表示する。
/// どのバフデバフカードが付いているかは他の人に分からないよう表示しない。
/// </summary>
public class ListingDisplay : MonoBehaviour
{
    [Tooltip("商品を表示するマット")]
    [SerializeField] private Transform mat;

    [Tooltip("出品したカードを置いていく山")]
    [SerializeField] private TableCardPile pile;

    [Tooltip("置いたカードを真上から見せるカメラの演出")]
    [SerializeField] private ListingCameraShot cameraShot;

    [SerializeField] private TMP_FontAsset font;

    [SerializeField] private float fontSize = 1.5f;

    [Tooltip("カードの下端（自分の席の側）から、最低出品額の文字の下端までの距離（文字はカードの側へ伸びる）")]
    [SerializeField] private float priceGap = 0.2f;

    [Tooltip("最低出品額を、いちばん上のカードからどれだけ浮かせるか")]
    [SerializeField] private float priceLift = 0.003f;

    [Tooltip("最低出品額の描画順。重なったカード（半透明）より後に描いて隠れないようにする")]
    [SerializeField] private int priceRenderQueue = 3900;

    [SerializeField] private float priceFadeSeconds = 0.4f;

    [Tooltip("最低出品額を表示してから、カメラを戻し始めるまでの秒数")]
    [SerializeField] private float holdSeconds = 1.5f;

    WorldLabel label;

    void Awake()
    {
        label = WorldLabel.Create("ListingLabel", transform, font, fontSize, Color.white);
        label.SetRenderQueue(priceRenderQueue);
        label.SetPivotBottom();
    }

    /// <summary>カードを置いて真上から見せ、最低出品額を表示してからカメラを戻す。終わるまで待つ。</summary>
    public IEnumerator Present(Listing listing)
    {
        label.Hide();
        TableCard card = pile.Place(listing.Goods.Data != null ? listing.Goods.Data.Parameter.CardSprite : null);

        Vector3 toSeat = -(pile.Facing * Vector3.forward);
        Vector3 pricePosition = mat.position;
        if (card != null)
        {
            yield return cameraShot.LookDown(card.transform, card.Length);
            pricePosition = card.transform.position + toSeat * (card.Length * 0.5f + priceGap);
        }
        pricePosition.y = pile.TopHeight + priceLift;

        label.SetAlpha(0f);
        label.Show(pricePosition, Quaternion.LookRotation(Vector3.down, -toSeat), PriceText(listing));
        for (float elapsed = 0f; elapsed < priceFadeSeconds; elapsed += Time.deltaTime)
        {
            label.SetAlpha(elapsed / priceFadeSeconds);
            yield return null;
        }
        label.SetAlpha(1f);

        yield return new WaitForSeconds(holdSeconds);
        if (cameraShot.TryGetReturnPosition(out Vector3 seat))
            StartCoroutine(StandLabel(seat, cameraShot.MoveSeconds));
        yield return cameraShot.Return();
    }

    /// <summary>最低出品額の数字。増額・値引きしていれば「初期価格+増額-値引き額」で表す</summary>
    static string PriceText(Listing listing)
    {
        if (listing.Raise == 0 && listing.Discount == 0)
            return listing.MinPrice.ToString();

        string text = listing.BasePrice.ToString();
        if (listing.Raise > 0)
            text += $"+{listing.Raise}";
        if (listing.Discount > 0)
            text += $"-{listing.Discount}";
        return text;
    }

    /// <summary>寝かせた文字を、下端を軸に席のカメラ（seat）の方へ起こす</summary>
    IEnumerator StandLabel(Vector3 seat, float seconds)
    {
        Transform target = label.transform;
        Quaternion from = target.rotation;
        Quaternion to = Quaternion.LookRotation(target.position - seat, Vector3.up);
        for (float elapsed = 0f; elapsed < seconds; elapsed += Time.deltaTime)
        {
            target.rotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0f, 1f, elapsed / seconds));
            yield return null;
        }
        target.rotation = to;
    }

    public void Hide()
    {
        label.Hide();
    }
}
