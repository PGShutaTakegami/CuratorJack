using System.Collections;
using UnityEngine;

/// <summary>
/// 他のプレイヤーが札を掲げたときの演出。入札 UI をフェードで消し、自分のカメラをその人の正面へ動かして
/// 頭と札をアップで映す。移動しきって少し待ってから札の数字を入札額まで数えて変え、少し止めてから元の視点へ戻り、入札 UI をフェードで出す。
/// 演出中に別の人が入札したら、元の視点へは戻らずにその人の正面へ続けて移動する（映さなかった人の札は最後に入札額にそろえる）。
/// </summary>
public class BidderCloseUpShot : MonoBehaviour
{
    [SerializeField] private PlayerRegistry registry;

    [SerializeField] private PlayerFrontCamera frontCamera;

    [SerializeField] private BidPaddleBoard paddles;

    [SerializeField] private TurnUI ui;

    [Tooltip("正面へ移動しきってから、札の数字を数え始めるまでの秒数")]
    [SerializeField] private float countDelaySeconds = 0.5f;

    [Tooltip("札の数字を変え終えてから、正面で止めておく秒数")]
    [SerializeField] private float holdSeconds = 1f;

    [Tooltip("映す中心（頭と札の板の中心の間）からカメラまでの距離（プレイヤーの大きさ倍）")]
    [SerializeField] private float distance = 1.3f;

    [Tooltip("映す中心から見たカメラの高さ（プレイヤーの大きさ倍）")]
    [SerializeField] private float height = 0.05f;

    int pendingBidder = PlayerRegistry.NoPlayer;
    Coroutine running;

    /// <summary>演出中（元の視点へ戻りきるまで）</summary>
    public bool IsPlaying => running != null;

    /// <summary>その人を正面からアップで映す。演出中なら、今の人の後に続けて映す</summary>
    public void Show(int bidder)
    {
        pendingBidder = bidder;
        if (running == null)
            running = StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        ui.HideAuction(true);

        while (pendingBidder != PlayerRegistry.NoPlayer)
        {
            int bidder = pendingBidder;
            pendingBidder = PlayerRegistry.NoPlayer;
            if (!registry.TryGet(bidder, out Transform player))
                continue;

            Vector3 Target() => paddles.TryGetBoardCenter(bidder, out Vector3 board)
                ? Vector3.Lerp(PlayerBody.HeadPosition(player), board, 0.5f)
                : PlayerBody.HeadPosition(player);

            yield return frontCamera.MoveToFront(player, Target, distance, height);
            yield return frontCamera.HoldFront(player, Target, distance, height, countDelaySeconds);
            paddles.StartCountUp(bidder);
            yield return frontCamera.HoldFrontWhile(player, Target, distance, height, () => paddles.IsCounting(bidder));
            yield return frontCamera.HoldFront(player, Target, distance, height, holdSeconds);
        }

        paddles.RevealAll();
        yield return frontCamera.Return();
        ui.HideAuction(false);
        running = null;
    }
}
