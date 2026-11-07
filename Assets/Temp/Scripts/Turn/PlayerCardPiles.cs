using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 落札したカードのコピーを、落札したプレイヤーの手元（マットの縁とテーブルの縁の間）に置いていく山。
/// 置いたカードはずっと残し、重ね方はマットの山と同じ。カードはそのプレイヤーの席から読める向きに置く。
/// </summary>
public class PlayerCardPiles : MonoBehaviour
{
    [Tooltip("重ね方とカードの見た目は、マットの山と同じものを使う")]
    [SerializeField] private TableCardPile pile;

    [SerializeField] private PlayerRegistry registry;

    [Tooltip("テーブル中央")]
    [SerializeField] private Transform tableCenter;

    [Tooltip("テーブル。縁の位置（半径）に使う")]
    [SerializeField] private Renderer table;

    [Tooltip("手元のカードを置く面（中央の台より低いテーブルの外周の上面）の高さの目印")]
    [SerializeField] private Transform surface;

    [Tooltip("中央のマット。手元の山はマットの外に置く")]
    [SerializeField] private Renderer mat;

    [Tooltip("マットの縁（0）からテーブルの縁（1）までのどこに置くか")]
    [Range(0f, 1f)]
    [SerializeField] private float edgeRatio = 0.5f;

    readonly Dictionary<int, int> counts = new Dictionary<int, int>();

    /// <summary>そのプレイヤーの手元にカードを置き、置いたカードを返す（置けなければ null）</summary>
    public TableCard Place(int playerNumber, Sprite sprite)
    {
        if (!TryGetSpot(playerNumber, out Pose spot))
            return null;

        counts.TryGetValue(playerNumber, out int layer);

        TableCard card = pile.PlaceAt(sprite, spot.position, surface.position.y, spot.rotation, layer);
        if (card != null)
            counts[playerNumber] = layer + 1;
        return card;
    }

    /// <summary>そのプレイヤーの手元の山の中心（置く面の高さ）と、席から読める向き（forward がテーブル中央側）</summary>
    public bool TryGetSpot(int playerNumber, out Pose spot)
    {
        spot = default;
        if (!registry.TryGet(playerNumber, out Transform player))
            return false;

        Vector3 center = tableCenter.position;
        Vector3 toPlayer = PlayerBody.HeadPosition(player) - center;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude < 1e-6f)
            return false;
        toPlayer.Normalize();

        float distance = Mathf.Lerp(mat.bounds.extents.x, table.bounds.extents.x, edgeRatio);
        Vector3 position = center + toPlayer * distance;
        position.y = surface.position.y;
        spot = new Pose(position, Quaternion.LookRotation(-toPlayer, Vector3.up));
        return true;
    }
}
