using System.Collections;
using UnityEngine;

/// <summary>
/// 落札が決まったときの演出。落札した人の正面へカメラを動かし（自分が落札したときは動かさない）、
/// 手元にカードのコピーをフェードで置き、胸のあたりに所持コインのリールを出して落札前から落札後の金額まで巻き、
/// リールを消して元の視点へ戻す。
/// </summary>
public class WinnerPresenter : MonoBehaviour
{
    [SerializeField] private PlayerRegistry registry;

    [SerializeField] private PlayerFrontCamera frontCamera;

    [SerializeField] private PlayerCardPiles cardPiles;

    [SerializeField] private CoinReelDisplay coinReels;

    [Tooltip("胸の位置。この骨と首の骨の間の chestToNeck の所")]
    [SerializeField] private string chestBone = "DEF.CHEST";

    [SerializeField] private string neckBone = "DEF.NECK";

    [Range(0f, 1f)]
    [SerializeField] private float chestToNeck = 0.8f;

    [Tooltip("映す中心（胸と頭の間）からカメラまでの距離（プレイヤーの大きさ倍）")]
    [SerializeField] private float distance = 1.4f;

    [Tooltip("映す中心から見たカメラの高さ（プレイヤーの大きさ倍）")]
    [SerializeField] private float height = 0.15f;

    [Tooltip("リールを出してから数字を変え始めるまでの秒数")]
    [SerializeField] private float beforeCountSeconds = 0.4f;

    [Tooltip("数字を変え終えてからリールを消すまでの秒数")]
    [SerializeField] private float afterCountSeconds = 1f;

    [Tooltip("リールが消えきるのを待つ秒数")]
    [SerializeField] private float hideSeconds = 0.3f;

    /// <summary>効果で所持コインが変わったときの演出。カードは置かず、その人のリールを前の額から後の額まで巻く</summary>
    public IEnumerator PresentCoins(int player, int coinsBefore, int coinsAfter) => Present(player, null, coinsBefore, coinsAfter);

    /// <summary>落札した人の演出を最後まで流す</summary>
    public IEnumerator Present(int winner, Sprite sprite, int coinsBefore, int coinsAfter)
    {
        if (!registry.TryGet(winner, out Transform player))
            yield break;

        bool isLocal = winner == registry.LocalPlayerNumber;
        Vector3 Chest() => Vector3.Lerp(PlayerBody.HeadPosition(player, chestBone), PlayerBody.HeadPosition(player, neckBone), chestToNeck);
        Vector3 Target() => Vector3.Lerp(Chest(), PlayerBody.HeadPosition(player), 0.5f);

        if (!isLocal)
            yield return frontCamera.MoveToFront(player, Target, distance, height);

        if (sprite != null)
            cardPiles.Place(winner, sprite);

        coinReels.Show(coinsBefore, frontCamera.LocalCamera, isLocal ? null : Chest);
        yield return new WaitForSeconds(beforeCountSeconds);
        yield return coinReels.CountTo(coinsAfter);
        yield return new WaitForSeconds(afterCountSeconds);

        coinReels.Hide();
        yield return new WaitForSeconds(hideSeconds);

        if (!isLocal)
            yield return frontCamera.Return();
    }
}
