using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// プレイヤーの頭の近くに文字を表示する（一定時間だけ：入札額など／消さずに：死亡の × など）。
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

    /// <summary>showSeconds だけ表示する</summary>
    public void Show(int playerNumber, string message)
    {
        if (TryPlace(playerNumber, message, out WorldLabel label))
            hiding[playerNumber] = StartCoroutine(HideLater(label));
    }

    /// <summary>消さずに表示し続ける（脱落の × など）</summary>
    public void Pin(int playerNumber, string message)
    {
        TryPlace(playerNumber, message, out _);
    }

    bool TryPlace(int playerNumber, string message, out WorldLabel label)
    {
        label = null;
        if (!registry.TryGet(playerNumber, out Transform player))
            return false;

        if (!labels.TryGetValue(playerNumber, out label))
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
        hiding.Remove(playerNumber);
        return true;
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
