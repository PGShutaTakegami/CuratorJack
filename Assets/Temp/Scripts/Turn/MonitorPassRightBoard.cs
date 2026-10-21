using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

/// <summary>
/// 自分以外の生きているプレイヤーの頭のモニターの画面に、残りの見送り権の枚数を表示する。
/// 文字は画面の少し手前に画面と同じ向きで置き、画面を動かす頭のボーンの子にして頭の動きについていかせる。
/// SetVisible(false) の間（オークション中など）は隠す。
/// </summary>
public class MonitorPassRightBoard : MonoBehaviour
{
    [SerializeField] private GameManager game;

    [SerializeField] private PlayerRegistry registry;

    [Tooltip("モニターの画面のオブジェクト名")]
    [SerializeField] private string screenName = "Monitor_Screen";

    [SerializeField] private TMP_FontAsset font;

    [SerializeField] private Color color = new Color(1f, 0.85f, 0.35f, 1f);

    [Tooltip("画面の大きさに対する、文字を収める枠の割合")]
    [Range(0.1f, 1f)]
    [SerializeField] private float fillRatio = 0.8f;

    [Tooltip("画面の面から手前へ浮かせる距離")]
    [SerializeField] private float surfaceOffset = 0.01f;

    readonly Dictionary<int, TextMeshPro> labels = new Dictionary<int, TextMeshPro>();
    bool visible = true;

    public void SetVisible(bool value)
    {
        visible = value;
    }

    void Update()
    {
        foreach (int playerNumber in registry.PlayerNumbers)
        {
            if (playerNumber == registry.LocalPlayerNumber)
                continue;

            bool show = visible && game.TryGet(playerNumber, out PlayerState player) && player.IsAlive;
            if (!labels.TryGetValue(playerNumber, out TextMeshPro label))
            {
                if (!show || !TryCreateLabel(playerNumber, out label))
                    continue;
                labels[playerNumber] = label;
            }

            label.gameObject.SetActive(show);
            if (show && game.TryGet(playerNumber, out PlayerState shown))
                label.text = shown.PassRights.ToString();
        }
    }

    void OnDestroy()
    {
        foreach (TextMeshPro label in labels.Values)
        {
            if (label != null)
                Destroy(label.gameObject);
        }
    }

    bool TryCreateLabel(int playerNumber, out TextMeshPro label)
    {
        label = null;
        if (!registry.TryGet(playerNumber, out Transform player))
            return false;

        SkinnedMeshRenderer screen = player.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(r => r.name == screenName);
        if (screen == null)
            return false;
        MeasureScreen(screen.bounds, PlayerBody.HeadPosition(player), out Vector3 center, out Vector3 normal, out Vector2 size);

        var go = new GameObject($"MonitorPassRight_Player{playerNumber}");
        label = go.AddComponent<TextMeshPro>();
        if (font != null)
            label.font = font;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.enableAutoSizing = true;
        label.fontSizeMin = 0.1f;
        label.fontSizeMax = 100f;
        label.rectTransform.sizeDelta = size * fillRatio;
        go.transform.SetPositionAndRotation(center + normal * surfaceOffset, Quaternion.LookRotation(-normal, Vector3.up));

        Transform head = screen.bones.FirstOrDefault(bone => bone != null && bone.name == PlayerBody.HeadBoneName);
        go.transform.SetParent(head != null ? head : screen.transform, true);
        return true;
    }

    /// <summary>
    /// 画面の描画範囲から、手前の面の中心・正面の向き・大きさ（横と縦）を求める。
    /// 正面は頭の中心から画面の中心へ向かう水平の向きとする（モデルは拡大されているため頂点からは求めない）。
    /// 画面は中央が手前へ膨らんでいるため、範囲の厚みの半分だけ手前を面とする。
    /// </summary>
    static void MeasureScreen(Bounds bounds, Vector3 head, out Vector3 center, out Vector3 normal, out Vector2 size)
    {
        Vector3 toScreen = bounds.center - head;
        toScreen.y = 0f;
        normal = toScreen.sqrMagnitude > 0f ? toScreen.normalized : Vector3.forward;
        float halfDepth = Mathf.Abs(bounds.extents.x * normal.x) + Mathf.Abs(bounds.extents.z * normal.z);
        center = bounds.center + normal * halfDepth;
        size = new Vector2(Mathf.Max(bounds.size.x, bounds.size.z), bounds.size.y);
    }
}
