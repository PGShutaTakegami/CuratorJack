using UnityEngine;

/// <summary>
/// 出品されたカードをマットの中心に置いていく山。置いたカードはずっと残し、
/// 新しいカードは少しずらして上に重ね、下のカードの一部が見えるようにする。
/// カードは自分（この画面のプレイヤー）の席から読める向きに置く。画面ごとの表示なので、
/// マルチではそれぞれの画面で、それぞれのプレイヤーの向きになる。
/// </summary>
public class TableCardPile : MonoBehaviour
{
    [Tooltip("カードを置くマット。上面の中心に置く")]
    [SerializeField] private Renderer mat;

    [SerializeField] private TableCardCatalog catalog;

    [Tooltip("カードの向きを決める、自分の席を調べるのに使う")]
    [SerializeField] private PlayerRegistry registry;

    [Tooltip("中心からずらす距離の範囲。下のカードの一部が見えるよう、0 にはしない")]
    [SerializeField] private Vector2 positionJitter = new Vector2(0.05f, 0.12f);

    [Tooltip("向きをずらす最大の角度")]
    [SerializeField] private float angleJitter = 20f;

    [Tooltip("マットの上面からの高さと、1 枚重ねるごとに上げる高さ")]
    [SerializeField] private float surfaceOffset = 0.002f;

    [SerializeField] private float layerStep = 0.001f;

    [SerializeField] private float fadeSeconds = 0.6f;

    int count;

    /// <summary>最後にカードを置いたときの、自分の席から見て正面の向き（forward が席から奥へ向く）</summary>
    public Quaternion Facing { get; private set; } = Quaternion.identity;

    /// <summary>自分の視点（カメラ）からマットの中心へ向かう水平の向き。カメラを演出で動かす前に調べる。</summary>
    Quaternion SeatFacing()
    {
        if (!registry.TryGet(registry.LocalPlayerNumber, out Transform localPlayer))
            return Facing;

        Camera seatCamera = localPlayer.GetComponentInChildren<Camera>();
        Vector3 seat = seatCamera != null ? seatCamera.transform.position : localPlayer.position;
        Vector3 away = mat.bounds.center - seat;
        away.y = 0f;
        return away.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(away, Vector3.up) : Facing;
    }

    /// <summary>次に置くカードの高さ（今いちばん上にあるカードより上）</summary>
    public float TopHeight => mat.bounds.max.y + surfaceOffset + count * layerStep;

    /// <summary>カードを置き、置いたカードを返す（対応するカードがなければ null）</summary>
    public TableCard Place(Sprite sprite)
    {
        Facing = SeatFacing();
        TableCard card = PlaceAt(sprite, mat.bounds.center, mat.bounds.max.y, Facing, count);
        if (card != null)
            count++;
        return card;
    }

    /// <summary>
    /// マットの山と同じ重ね方（少しずらし・少し回す）で、center の周りの surfaceY の面に layer 枚目として置く。
    /// facing はカードの上（forward）の向き。対応するカードがなければ null
    /// </summary>
    public TableCard PlaceAt(Sprite sprite, Vector3 center, float surfaceY, Quaternion facing, int layer)
    {
        TableCard prefab = catalog.Find(sprite);
        if (prefab == null)
            return null;

        float direction = Random.Range(0f, Mathf.PI * 2f);
        Vector2 offset = new Vector2(Mathf.Cos(direction), Mathf.Sin(direction)) * Random.Range(positionJitter.x, positionJitter.y);
        Vector3 position = new Vector3(center.x + offset.x, surfaceY + surfaceOffset + layer * layerStep, center.z + offset.y);
        Quaternion rotation = facing * Quaternion.Euler(0f, Random.Range(-angleJitter, angleJitter), 0f);

        TableCard card = Instantiate(prefab, position, rotation, transform);
        card.FadeIn(fadeSeconds, layer);
        return card;
    }
}
