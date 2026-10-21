using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 自分の見送り権の枚数だけ、チケットを上部のバーの左側に並べる。少しずつ重ねて斜めに並べ、右のものほど手前に置く。
/// 見送り権が減ったら手前（右端）のチケットからフェードで消し、増えたら右に足す。生きていないときは何も出さない。
/// </summary>
public class PassTicketStack : MonoBehaviour
{
    [SerializeField] private GameManager game;

    [SerializeField] private PlayerRegistry registry;

    [Tooltip("並べるチケットの画像（チケット全体）")]
    [SerializeField] private Sprite ticketSprite;

    [SerializeField] private float ticketWidth = 180f;

    [Tooltip("隣のチケットとの横の間隔（幅より小さいと重なる）")]
    [SerializeField] private float spacing = 100f;

    [Tooltip("チケットの傾き（度。プラスで反時計回り）")]
    [SerializeField] private float angle = 45f;

    [SerializeField] private float fadeSeconds = 0.4f;

    readonly List<Image> tickets = new List<Image>();

    void Update()
    {
        int count = LocalPassRights();
        while (tickets.Count < count)
            tickets.Add(CreateTicket(tickets.Count));
        while (tickets.Count > count)
        {
            Image last = tickets[tickets.Count - 1];
            tickets.RemoveAt(tickets.Count - 1);
            StartCoroutine(FadeOut(last));
        }
    }

    int LocalPassRights()
    {
        return game.TryGet(registry.LocalPlayerNumber, out PlayerState player) && player.IsAlive ? player.PassRights : 0;
    }

    Image CreateTicket(int index)
    {
        var ticket = new GameObject($"Ticket{index + 1}", typeof(RectTransform), typeof(Image));
        ticket.layer = gameObject.layer;
        var rect = (RectTransform)ticket.transform;
        rect.SetParent(transform, false);
        rect.anchorMin = rect.anchorMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(ticketWidth, ticketWidth * ticketSprite.rect.height / ticketSprite.rect.width);
        rect.anchoredPosition = new Vector2(index * spacing, 0f);
        rect.localRotation = Quaternion.Euler(0f, 0f, angle);
        rect.SetAsLastSibling();

        var image = ticket.GetComponent<Image>();
        image.sprite = ticketSprite;
        image.raycastTarget = false;
        return image;
    }

    IEnumerator FadeOut(Image ticket)
    {
        Color color = ticket.color;
        for (float elapsed = 0f; elapsed < fadeSeconds; elapsed += Time.deltaTime)
        {
            color.a = 1f - Mathf.SmoothStep(0f, 1f, elapsed / fadeSeconds);
            ticket.color = color;
            yield return null;
        }
        Destroy(ticket.gameObject);
    }
}
