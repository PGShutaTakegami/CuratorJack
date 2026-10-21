using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// ゲームが開始されたら、告知を順番にフェードで表示し、一定時間後にフェードで消す。
/// 告知は開始まで非表示のため、このコンポーネントは常に有効なオブジェクトに付ける。
/// </summary>
public class GameStartAnnouncer : MonoBehaviour
{
    [SerializeField] private GameStartGate startGate;

    [Tooltip("告知の背景。フェードの対象")]
    [SerializeField] private CanvasGroupFader panel;

    [Tooltip("背景の中で順番に表示する告知の文字")]
    [SerializeField] private GameObject[] messages;

    [Tooltip("フェードインしてからフェードアウトを始めるまでの秒数")]
    [SerializeField] private float displaySeconds = 1f;

    [Tooltip("告知が消えてから次の告知を出すまでの秒数")]
    [SerializeField] private float intervalSeconds = 1f;

    /// <summary>告知を出し始めた（フェードイン開始時）。表示した告知の文字を渡す</summary>
    public event Action<GameObject> MessageShown;

    void Awake()
    {
        startGate.Started += Announce;
    }

    void OnDestroy()
    {
        startGate.Started -= Announce;
    }

    void Announce()
    {
        StartCoroutine(ShowMessagesInOrder());
    }

    IEnumerator ShowMessagesInOrder()
    {
        for (int i = 0; i < messages.Length; i++)
        {
            if (i > 0)
                yield return new WaitForSeconds(intervalSeconds);

            yield return ShowMessage(messages[i]);
        }
    }

    /// <summary>
    /// 告知を 1 つ表示して消す（ゲーム途中の告知用）。
    /// messages にない告知（同じ背景の中の文字）も表示でき、消えたら非表示に戻す。
    /// </summary>
    public IEnumerator Show(GameObject message)
    {
        yield return ShowMessage(message);
        message.SetActive(false);
    }

    IEnumerator ShowMessage(GameObject message)
    {
        foreach (GameObject other in messages)
            other.SetActive(false);
        message.SetActive(true);

        panel.GetComponent<CanvasGroup>().alpha = 0f;
        panel.gameObject.SetActive(true);

        bool isFaded = false;
        panel.FadeIn(() => isFaded = true);
        MessageShown?.Invoke(message);
        yield return new WaitUntil(() => isFaded);

        yield return new WaitForSeconds(displaySeconds);

        isFaded = false;
        panel.FadeOut(() => isFaded = true);
        yield return new WaitUntil(() => isFaded);

        panel.gameObject.SetActive(false);
    }
}
