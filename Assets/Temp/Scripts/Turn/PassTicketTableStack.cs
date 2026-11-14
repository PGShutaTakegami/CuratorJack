using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 各プレイヤーの手元（落札カードの山の、その人の席から見て左）に、今の見送り権の枚数だけチケットを並べ、残りの HP が机の上で分かるようにする。
/// 少しずつ重ねて席から見て左へ並べ、増えたら上から落として足し、減ったら最後に置いたものから浮かせて消す。生きていない人の分は置かない。
/// 見送り権はゲームの状態から毎フレーム合わせるため、マルチでもそれぞれの画面で同じ枚数になる。
/// </summary>
public class PassTicketTableStack : MonoBehaviour
{
    [SerializeField] private GameManager game;

    [SerializeField] private PlayerRegistry registry;

    [Tooltip("置く位置の基準にする、その人の落札カードの山")]
    [SerializeField] private PlayerCardPiles piles;

    [Tooltip("置くチケット（寝かせた板が上を向き、チケットの上が forward を向くもの）")]
    [SerializeField] private GameObject ticketPrefab;

    [Tooltip("落札カードの山から、その人の席から見て左へずらす距離（1 枚目の中心）")]
    [SerializeField] private float sideOffset = 0.6f;

    [Tooltip("並べるチケットどうしの、その人の席から見て左への間隔（チケットの幅より小さいと重なる）")]
    [SerializeField] private float spacing = 0.2f;

    [Tooltip("重ねるチケット 1 枚ごとに上げる高さ")]
    [SerializeField] private float layerHeight = 0.002f;

    [Tooltip("向きをずらす最大の角度")]
    [SerializeField] private float angleJitter = 6f;

    [Tooltip("面からの高さ")]
    [SerializeField] private float surfaceOffset = 0.002f;

    [Tooltip("落ち始める高さ（置く位置から上へ）")]
    [SerializeField] private float dropHeight = 0.25f;

    [SerializeField] private float dropSeconds = 0.4f;

    [Tooltip("消えるときに浮く高さ")]
    [SerializeField] private float liftHeight = 0.15f;

    [SerializeField] private float vanishSeconds = 0.4f;

    readonly Dictionary<int, List<Transform>> stacks = new Dictionary<int, List<Transform>>();

    void Update()
    {
        foreach (int playerNumber in registry.PlayerNumbers)
            Sync(playerNumber, PassRightsOf(playerNumber));
    }

    int PassRightsOf(int playerNumber)
    {
        return game.TryGet(playerNumber, out PlayerState player) && player.IsAlive ? player.PassRights : 0;
    }

    void Sync(int playerNumber, int count)
    {
        if (!stacks.TryGetValue(playerNumber, out List<Transform> tickets))
        {
            tickets = new List<Transform>();
            stacks[playerNumber] = tickets;
        }

        while (tickets.Count < count && piles.TryGetSpot(playerNumber, out Pose spot))
            tickets.Add(Place(spot, tickets.Count));

        while (tickets.Count > count)
        {
            Transform last = tickets[tickets.Count - 1];
            tickets.RemoveAt(tickets.Count - 1);
            if (last != null)
                StartCoroutine(Vanish(last));
        }
    }

    Transform Place(Pose spot, int index)
    {
        Vector3 position = spot.position
            - spot.rotation * Vector3.right * (sideOffset + index * spacing)
            + Vector3.up * (surfaceOffset + index * layerHeight);
        Quaternion rotation = spot.rotation * Quaternion.Euler(0f, Random.Range(-angleJitter, angleJitter), 0f);

        Transform ticket = Instantiate(ticketPrefab, position + Vector3.up * dropHeight, rotation, transform).transform;
        StartCoroutine(Drop(ticket, position));
        return ticket;
    }

    /// <summary>上から落ちてきて置かれる（落ちるほど速くなる）</summary>
    IEnumerator Drop(Transform ticket, Vector3 spot)
    {
        Vector3 start = spot + Vector3.up * dropHeight;
        for (float elapsed = 0f; elapsed < dropSeconds; elapsed += Time.deltaTime)
        {
            if (ticket == null)
                yield break;
            float t = elapsed / dropSeconds;
            ticket.position = Vector3.LerpUnclamped(start, spot, t * t);
            yield return null;
        }
        if (ticket != null)
            ticket.position = spot;
    }

    /// <summary>浮きながら小さくなって消える</summary>
    IEnumerator Vanish(Transform ticket)
    {
        Vector3 start = ticket.position;
        Vector3 scale = ticket.localScale;
        for (float elapsed = 0f; elapsed < vanishSeconds; elapsed += Time.deltaTime)
        {
            if (ticket == null)
                yield break;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / vanishSeconds);
            ticket.position = start + Vector3.up * liftHeight * t;
            ticket.localScale = scale * (1f - t);
            yield return null;
        }
        if (ticket != null)
            Destroy(ticket.gameObject);
    }
}
