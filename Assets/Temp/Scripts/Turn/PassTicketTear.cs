using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 自分の見送り権が使われたときに、画面中央にチケットを出して半券を切り離す演出。
/// フェードインしたら半券を小さく揺らし、点線のところで上から裂いて半券を右下へ落とし、フェードアウトする。
/// 一度に複数枚使われたときは、その枚数のチケットを縦に並べて同時に切り離す。
/// 進行は止めない。続けて使われたときは 1 回ずつ順に見せる。
/// </summary>
public class PassTicketTear : MonoBehaviour
{
    [SerializeField] private GameManager game;

    [SerializeField] private PlayerRegistry registry;

    [SerializeField] private CanvasGroup group;

    [Tooltip("チケットの本体（点線より左）")]
    [SerializeField] private Image mainImage;

    [Tooltip("チケットの半券（点線より右）")]
    [SerializeField] private Image stubImage;

    [Tooltip("本体と半券を並べたときのチケット全体の幅")]
    [SerializeField] private float ticketWidth = 760f;

    [Tooltip("複数枚を同時に見せるときの、チケット同士の縦の隙間")]
    [SerializeField] private float ticketGap = 40f;

    [SerializeField] private float fadeInSeconds = 0.3f;

    [Tooltip("フェードインしてから、半券を揺らし始めるまでの秒数")]
    [SerializeField] private float holdSeconds = 0.3f;

    [Tooltip("半券を引っぱるように揺らす秒数")]
    [SerializeField] private float tugSeconds = 0.3f;

    [SerializeField] private float tugAngle = 3f;

    [Tooltip("点線の上から裂けていく秒数")]
    [SerializeField] private float tearSeconds = 0.25f;

    [Tooltip("裂けたときの半券の傾き（度。マイナスで時計回り）")]
    [SerializeField] private float tearAngle = -12f;

    [Tooltip("半券が外れて落ちていく秒数")]
    [SerializeField] private float detachSeconds = 0.5f;

    [Tooltip("外れた半券が動く距離")]
    [SerializeField] private Vector2 stubDetachOffset = new Vector2(140f, -110f);

    [SerializeField] private float stubDetachAngle = -30f;

    [Tooltip("半券が外れたときに本体が動く距離")]
    [SerializeField] private Vector2 mainDetachOffset = new Vector2(-30f, 0f);

    [SerializeField] private float mainDetachAngle = 3f;

    [Tooltip("切り離してから、フェードアウトを始めるまでの秒数")]
    [SerializeField] private float afterSeconds = 0.4f;

    [SerializeField] private float fadeOutSeconds = 0.4f;

    /// <summary>本体と半券 1 組と、それぞれの元の位置</summary>
    sealed class Ticket
    {
        public RectTransform Main;
        public RectTransform Stub;
        public Vector2 MainHome;
        public Vector2 StubHome;

        public void ResetPose()
        {
            Pose(Main, MainHome, 0f);
            Pose(Stub, StubHome, 0f);
        }
    }

    readonly List<Ticket> tickets = new List<Ticket>();
    readonly Queue<int> pending = new Queue<int>();
    Vector2 mainSize;
    Vector2 stubSize;
    int shownCount;
    Coroutine running;

    void Awake()
    {
        group.alpha = 0f;
        group.blocksRaycasts = false;
        Shape();
        tickets.Add(new Ticket { Main = mainImage.rectTransform, Stub = stubImage.rectTransform });
        Arrange(1);
        game.PassRightConsumed += HandlePassRightConsumed;
    }

    void OnDestroy()
    {
        game.PassRightConsumed -= HandlePassRightConsumed;
    }

    /// <summary>
    /// 本体と半券を元の 1 枚の画像と同じ比率の大きさにする。
    /// 半券は左下（点線の下端）を軸にして、上から裂けるように回す。
    /// </summary>
    void Shape()
    {
        Rect mainRect = mainImage.sprite.rect;
        Rect stubRect = stubImage.sprite.rect;
        float scale = ticketWidth / (mainRect.width + stubRect.width);
        mainSize = mainRect.size * scale;
        stubSize = stubRect.size * scale;

        RectTransform main = mainImage.rectTransform;
        main.anchorMin = main.anchorMax = new Vector2(0.5f, 0.5f);
        main.pivot = new Vector2(0.5f, 0.5f);
        main.sizeDelta = mainSize;

        RectTransform stub = stubImage.rectTransform;
        stub.anchorMin = stub.anchorMax = new Vector2(0.5f, 0.5f);
        stub.pivot = new Vector2(0f, 0f);
        stub.sizeDelta = stubSize;
    }

    /// <summary>count 組のチケットを、それぞれ本体と半券を横に並べた形で、中央に縦に並べる（足りない組は複製する）</summary>
    void Arrange(int count)
    {
        while (tickets.Count < count)
        {
            tickets.Add(new Ticket
            {
                Main = Instantiate(mainImage, mainImage.transform.parent).rectTransform,
                Stub = Instantiate(stubImage, stubImage.transform.parent).rectTransform,
            });
        }

        float left = -ticketWidth * 0.5f;
        float rowHeight = Mathf.Max(mainSize.y, stubSize.y) + ticketGap;
        for (int i = 0; i < tickets.Count; i++)
        {
            Ticket ticket = tickets[i];
            bool isShown = i < count;
            ticket.Main.gameObject.SetActive(isShown);
            ticket.Stub.gameObject.SetActive(isShown);
            float y = ((count - 1) * 0.5f - i) * rowHeight;
            ticket.MainHome = new Vector2(left + mainSize.x * 0.5f, y);
            ticket.StubHome = new Vector2(left + mainSize.x, y - stubSize.y * 0.5f);
            ticket.ResetPose();
        }
        shownCount = count;
    }

    void ForEachShown(System.Action<Ticket> apply)
    {
        for (int i = 0; i < shownCount; i++)
            apply(tickets[i]);
    }

    static void Pose(RectTransform target, Vector2 position, float angle)
    {
        target.anchoredPosition = position;
        target.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    void HandlePassRightConsumed(PlayerState player, int count)
    {
        if (player.PlayerNumber != registry.LocalPlayerNumber)
            return;

        pending.Enqueue(count);
        if (running == null)
            running = StartCoroutine(PlayPending());
    }

    IEnumerator PlayPending()
    {
        while (pending.Count > 0)
            yield return Play(pending.Dequeue());
        running = null;
    }

    IEnumerator Play(int count)
    {
        Arrange(count);

        yield return Tween(fadeInSeconds, t => group.alpha = t);
        yield return new WaitForSeconds(holdSeconds);
        yield return Tween(tugSeconds, t => ForEachShown(ticket =>
            Pose(ticket.Stub, ticket.StubHome, Mathf.Sin(t * Mathf.PI * 4f) * tugAngle * (1f - t))));
        yield return Tween(tearSeconds, t => ForEachShown(ticket => Pose(ticket.Stub, ticket.StubHome, tearAngle * t)));
        yield return Tween(detachSeconds, t => ForEachShown(ticket =>
        {
            Pose(ticket.Stub, ticket.StubHome + stubDetachOffset * t, Mathf.Lerp(tearAngle, stubDetachAngle, t));
            Pose(ticket.Main, ticket.MainHome + mainDetachOffset * t, mainDetachAngle * t);
        }));
        yield return new WaitForSeconds(afterSeconds);
        yield return Tween(fadeOutSeconds, t => group.alpha = 1f - t);
        ForEachShown(ticket => ticket.ResetPose());
    }

    static IEnumerator Tween(float seconds, System.Action<float> apply)
    {
        for (float elapsed = 0f; elapsed < seconds; elapsed += Time.deltaTime)
        {
            apply(Mathf.SmoothStep(0f, 1f, elapsed / seconds));
            yield return null;
        }
        apply(1f);
    }
}
