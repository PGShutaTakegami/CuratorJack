using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// プレイヤーの頭の近くに、一定時間だけ文字を表示する（入札額・死亡の × など）。
/// 表示位置は頭からテーブル中央へ forwardOffset、上へ upOffset ずらした所。
/// </summary>
public class PlayerLabelBoard : MonoBehaviour
{
    [SerializeField] private PlayerRegistry registry;

    [Tooltip("プレイヤーの前を決める基準（テーブル中央）")]
    [SerializeField] private Transform tableCenter;

    [SerializeField] private TMP_FontAsset font;

    [SerializeField] private float fontSize = 1.5f;

    [SerializeField] private Color color = Color.white;

    [SerializeField] private float forwardOffset = 0.5f;

    [SerializeField] private float upOffset = 0.3f;

    [Tooltip("表示しておく秒数")]
    [SerializeField] private float showSeconds = 2f;

    readonly Dictionary<int, WorldLabel> labels = new Dictionary<int, WorldLabel>();
    readonly Dictionary<int, Coroutine> hiding = new Dictionary<int, Coroutine>();

    public void Show(int playerNumber, string message)
    {
        if (!registry.TryGet(playerNumber, out Transform player))
            return;

        if (!labels.TryGetValue(playerNumber, out WorldLabel label))
        {
            label = WorldLabel.Create($"Label_Player{playerNumber}", transform, font, fontSize, color);
            labels[playerNumber] = label;
        }

        Vector3 head = PlayerBody.HeadPosition(player);
        Vector3 toCenter = tableCenter.position - head;
        toCenter.y = 0f;
        Vector3 position = head + toCenter.normalized * forwardOffset + Vector3.up * upOffset;
        label.Show(position, message);

        if (hiding.TryGetValue(playerNumber, out Coroutine running) && running != null)
            StopCoroutine(running);
        hiding[playerNumber] = StartCoroutine(HideLater(label));
    }

    public void HideAll()
    {
        StopAllCoroutines();
        hiding.Clear();
        foreach (WorldLabel label in labels.Values)
            label.Hide();
    }

    IEnumerator HideLater(WorldLabel label)
    {
        yield return new WaitForSeconds(showSeconds);
        label.Hide();
    }
}
