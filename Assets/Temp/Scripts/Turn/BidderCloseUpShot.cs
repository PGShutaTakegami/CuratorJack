using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// プレイヤーが札を掲げたときの演出。入札 UI をフェードで消し、自分のカメラをその人の正面へ動かして
/// 頭と札をアップで映す。移動しきって少し待ってから札の数字を入札額まで数えて変え、少し止めてから元の視点へ戻り、入札 UI をフェードで出す。
/// 自分が入札したときは、カメラが自分の頭から出て正面に着いてから自分の札を掲げ（札がカメラの目の前を覆わないように）、
/// 頭のモニターに自分のアイコンを出して、頭と札が入るように映し直す。
/// 演出中に別の人が入札したら、元の視点へは戻らずにその人の正面へ続けて移動する（映さなかった人の札は最後に入札額にそろえる）。
/// 手元のテーブルの場所（アイテムなど）を映す頼みも同じ順番待ちで受け、入札のアップより先に頼まれた順に映す。
/// 自分の手元のときは席から動かずに見下ろし、ほかの人の手元のときはその人の正面側から映す。
/// </summary>
public class BidderCloseUpShot : MonoBehaviour
{
    [SerializeField] private PlayerRegistry registry;

    [SerializeField] private PlayerFrontCamera frontCamera;

    [SerializeField] private BidPaddleBoard paddles;

    [Tooltip("自分を映している間、自分の頭のモニターにも自分のアイコンを出す")]
    [SerializeField] private MonitorIconBoard monitorIcons;

    [SerializeField] private TurnUI ui;

    [Tooltip("正面へ移動しきってから、札の数字を数え始めるまでの秒数")]
    [SerializeField] private float countDelaySeconds = 0.5f;

    [Tooltip("札の数字を変え終えてから、正面で止めておく秒数")]
    [SerializeField] private float holdSeconds = 1f;

    [Tooltip("映す中心（頭と札の板の中心の間）からカメラまでの距離（プレイヤーの大きさ倍）")]
    [SerializeField] private float distance = 1.3f;

    [Tooltip("映す中心から見たカメラの高さ（プレイヤーの大きさ倍）")]
    [SerializeField] private float height = 0.05f;

    [Tooltip("手元のテーブルの場所を映すときの、その場所からカメラまでの距離（プレイヤーの大きさ倍）")]
    [SerializeField] private float tableDistance = 0.8f;

    [Tooltip("手元のテーブルの場所を映すときの、その場所から見たカメラの高さ（プレイヤーの大きさ倍）")]
    [SerializeField] private float tableHeight = 0.6f;

    readonly struct TableShot
    {
        public readonly int player;
        public readonly Vector3 spot;
        public readonly Func<IEnumerator> whileShown;

        public TableShot(int player, Vector3 spot, Func<IEnumerator> whileShown)
        {
            this.player = player;
            this.spot = spot;
            this.whileShown = whileShown;
        }
    }

    readonly Queue<TableShot> tableShots = new Queue<TableShot>();
    int pendingBidder = PlayerRegistry.NoPlayer;
    int pendingAmount;
    Coroutine running;

    /// <summary>演出中（元の視点へ戻りきるまで）</summary>
    public bool IsPlaying => running != null;

    /// <summary>その人を正面からアップで映す。演出中なら、今の人の後に続けて映す。amount は自分の札に書く入札額</summary>
    public void Show(int bidder, int amount)
    {
        pendingBidder = bidder;
        pendingAmount = amount;
        if (running == null)
            running = StartCoroutine(Run());
    }

    /// <summary>
    /// その人の手元のテーブルの spot を映し（自分なら席から見下ろし、ほかの人ならその人の正面側から）、着いたら whileShown の演出が終わるまで映し続ける。
    /// 演出中なら、今の演出の後に続けて映す
    /// </summary>
    public void ShowTableSpot(int player, Vector3 spot, Func<IEnumerator> whileShown)
    {
        tableShots.Enqueue(new TableShot(player, spot, whileShown));
        if (running == null)
            running = StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        ui.HideAuction(true);

        while (tableShots.Count > 0 || pendingBidder != PlayerRegistry.NoPlayer)
        {
            if (tableShots.Count > 0)
            {
                TableShot shot = tableShots.Dequeue();
                if (!registry.TryGet(shot.player, out Transform owner))
                {
                    yield return shot.whileShown();
                    continue;
                }

                Vector3 Spot() => shot.spot;
                if (shot.player == registry.LocalPlayerNumber)
                {
                    yield return frontCamera.LookFromSeat(Spot);
                    yield return frontCamera.HoldLookFromSeatWhile(Spot, Playing(shot.whileShown()));
                    continue;
                }

                yield return frontCamera.MoveToFront(owner, Spot, tableDistance, tableHeight);
                yield return frontCamera.HoldFrontWhile(owner, Spot, tableDistance, tableHeight, Playing(shot.whileShown()));
                continue;
            }

            int bidder = pendingBidder;
            int amount = pendingAmount;
            pendingBidder = PlayerRegistry.NoPlayer;
            if (!registry.TryGet(bidder, out Transform player))
                continue;

            Vector3 Head() => PlayerBody.HeadPosition(player);
            Vector3 Target() => paddles.TryGetBoardCenter(bidder, out Vector3 board)
                ? Vector3.Lerp(PlayerBody.HeadPosition(player), board, 0.5f)
                : PlayerBody.HeadPosition(player);

            if (bidder == registry.LocalPlayerNumber)
            {
                yield return frontCamera.MoveToFront(player, Head, distance, height);
                paddles.ShowLocal(amount);
                monitorIcons.ShowsLocal = true;
                yield return frontCamera.MoveToFront(player, Target, distance, height);
                yield return frontCamera.HoldFront(player, Target, distance, height, holdSeconds);
                paddles.HideLocal();
                monitorIcons.ShowsLocal = false;
                continue;
            }

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

    /// <summary>演出を始め、終わるまで true を返す（HoldFrontWhile で映し続けるため）</summary>
    Func<bool> Playing(IEnumerator routine)
    {
        bool isPlaying = true;
        StartCoroutine(Play());
        return () => isPlaying;

        IEnumerator Play()
        {
            yield return routine;
            isPlaying = false;
        }
    }
}
