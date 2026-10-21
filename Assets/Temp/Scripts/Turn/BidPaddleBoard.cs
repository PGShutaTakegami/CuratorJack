using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

/// <summary>
/// オークションで、入札したプレイヤーに札（入札額を書いた板）を掲げさせる。
/// 札はプレイヤーごとに初めて使うときに作り、以後は使い回す。自分の札は自分の画面には出さない（他の人の画面に出る）。
/// 数字をすぐ変えずに掲げておき（RaiseLater）、あとで今の数字から入札額まで 1 ずつ数えて変える（StartCountUp）こともできる。
/// </summary>
public class BidPaddleBoard : MonoBehaviour
{
    [SerializeField] private PlayerRegistry registry;

    [Tooltip("プレイヤーの前を決める基準（テーブル中央）")]
    [SerializeField] private Transform tableCenter;

    [Tooltip("手に持つ札のモデル")]
    [SerializeField] private GameObject paddlePrefab;

    [SerializeField] private TMP_FontAsset font;

    [SerializeField] private Color numberColor = new Color(0.25f, 0.16f, 0.08f);

    [SerializeField] private BidPaddleMotion motion = new BidPaddleMotion();

    [Tooltip("自分の札も自分の画面に出すか（カメラの目の前に来て画面を覆うため、通常は出さない）")]
    [SerializeField] private bool showLocalPlayer;

    [Tooltip("数えて変えるときに、1 つ進める間隔（秒）")]
    [SerializeField] private float countStepSeconds = 0.06f;

    [Tooltip("数えて変えるのにかける最長の秒数（差が大きいときは間隔を詰める）")]
    [SerializeField] private float maxCountSeconds = 1.2f;

    readonly Dictionary<int, BidPaddle> paddles = new Dictionary<int, BidPaddle>();
    readonly Dictionary<int, int> shownAmounts = new Dictionary<int, int>();
    readonly Dictionary<int, int> targetAmounts = new Dictionary<int, int>();
    readonly Dictionary<int, Coroutine> counting = new Dictionary<int, Coroutine>();
    int lastAmount;

    /// <summary>そのプレイヤーに金額を書いた札を掲げさせる</summary>
    public void Raise(int playerNumber, int amount)
    {
        lastAmount = amount;
        if (!TryGetShownPaddle(playerNumber, out BidPaddle paddle))
            return;

        StopCountUp(playerNumber);
        shownAmounts[playerNumber] = amount;
        targetAmounts[playerNumber] = amount;
        paddle.Raise(amount.ToString());
    }

    /// <summary>
    /// 札を掲げるが、数字はまだ入札額に変えない（StartCountUp で数えて変える）。
    /// 掲げていなかった札には直前の入札額（なければ入札額）を出しておく
    /// </summary>
    public void RaiseLater(int playerNumber, int amount)
    {
        int previous = lastAmount > 0 ? lastAmount : amount;
        lastAmount = amount;
        if (!TryGetShownPaddle(playerNumber, out BidPaddle paddle))
            return;

        targetAmounts[playerNumber] = amount;
        if (!paddle.IsShown || !shownAmounts.ContainsKey(playerNumber))
        {
            shownAmounts[playerNumber] = previous;
            paddle.Raise(previous.ToString());
        }
    }

    /// <summary>札の数字を、今の数字から入札額まで 1 ずつ数えて変え始める（数えている間に入札額が変わったらそこまで数える）</summary>
    public void StartCountUp(int playerNumber)
    {
        if (IsCounting(playerNumber) || !IsPending(playerNumber) || !paddles.TryGetValue(playerNumber, out BidPaddle paddle) || paddle == null)
            return;

        counting[playerNumber] = StartCoroutine(CountUp(playerNumber));
    }

    public bool IsCounting(int playerNumber) => counting.ContainsKey(playerNumber);

    /// <summary>まだ入札額に変えていない札を、数えずにすぐ入札額にする</summary>
    public void RevealAll()
    {
        foreach (int playerNumber in targetAmounts.Keys.Where(IsPending).ToArray())
        {
            StopCountUp(playerNumber);
            shownAmounts[playerNumber] = targetAmounts[playerNumber];
            if (paddles.TryGetValue(playerNumber, out BidPaddle paddle) && paddle != null)
                paddle.Raise(targetAmounts[playerNumber].ToString());
        }
    }

    public void Lower(int playerNumber)
    {
        if (paddles.TryGetValue(playerNumber, out BidPaddle paddle))
            paddle.Lower();
    }

    /// <summary>keep 以外の全員の札を下ろす</summary>
    public void LowerAllExcept(int keep)
    {
        foreach (KeyValuePair<int, BidPaddle> pair in paddles.Where(pair => pair.Key != keep))
            pair.Value.Lower();
    }

    /// <summary>全員の札を下ろし、次のオークションのために数字の記録を消す</summary>
    public void LowerAll()
    {
        LowerAllExcept(PlayerRegistry.NoPlayer);
        foreach (int playerNumber in counting.Keys.ToArray())
            StopCountUp(playerNumber);
        shownAmounts.Clear();
        targetAmounts.Clear();
        lastAmount = 0;
    }

    bool IsPending(int playerNumber) =>
        targetAmounts.TryGetValue(playerNumber, out int target)
        && (!shownAmounts.TryGetValue(playerNumber, out int shown) || shown != target);

    IEnumerator CountUp(int playerNumber)
    {
        while (IsPending(playerNumber) && paddles.TryGetValue(playerNumber, out BidPaddle paddle) && paddle != null)
        {
            int target = targetAmounts[playerNumber];
            int shown = shownAmounts[playerNumber];
            int remaining = Mathf.Abs(target - shown);
            float interval = Mathf.Min(countStepSeconds, maxCountSeconds / remaining);
            yield return new WaitForSeconds(interval);

            target = targetAmounts[playerNumber];
            shown += shown < target ? 1 : -1;
            shownAmounts[playerNumber] = shown;
            if (shown == target)
                paddle.Raise(shown.ToString());
            else
                paddle.SetNumber(shown.ToString());
        }
        counting.Remove(playerNumber);
    }

    void StopCountUp(int playerNumber)
    {
        if (counting.TryGetValue(playerNumber, out Coroutine routine) && routine != null)
            StopCoroutine(routine);
        counting.Remove(playerNumber);
    }

    /// <summary>この画面に出す札があれば返す（自分の札は showLocalPlayer のときだけ）</summary>
    bool TryGetShownPaddle(int playerNumber, out BidPaddle paddle)
    {
        paddle = null;
        return (playerNumber != registry.LocalPlayerNumber || showLocalPlayer) && TryGetPaddle(playerNumber, out paddle);
    }

    /// <summary>そのプレイヤーが札を出しているなら、数字を描いた板の中心を返す</summary>
    public bool TryGetBoardCenter(int playerNumber, out Vector3 center)
    {
        center = Vector3.zero;
        if (!paddles.TryGetValue(playerNumber, out BidPaddle paddle) || paddle == null || !paddle.IsShown)
            return false;

        center = paddle.BoardCenter;
        return true;
    }

    bool TryGetPaddle(int playerNumber, out BidPaddle paddle)
    {
        if (paddles.TryGetValue(playerNumber, out paddle) && paddle != null)
            return true;

        if (!registry.TryGet(playerNumber, out Transform player))
            return false;

        paddle = BidPaddle.Create(player, tableCenter.position, paddlePrefab, font, numberColor, motion, transform);
        paddles[playerNumber] = paddle;
        return true;
    }
}
