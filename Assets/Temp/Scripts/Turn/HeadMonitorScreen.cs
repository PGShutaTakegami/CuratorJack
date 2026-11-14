using System.Linq;
using UnityEngine;

/// <summary>
/// プレイヤーの頭のモニターの画面に物を表示するための置き場を作る。
/// 置き場は画面の少し手前に画面と同じ向き（ローカルの +Z が画面の奥）で置き、画面を動かす頭のボーンの子にして頭の動きについていかせる。
/// </summary>
public static class HeadMonitorScreen
{
    /// <summary>置き場を作り、画面の大きさ（横と縦）を返す。画面が見つからなければ false</summary>
    public static bool TryCreateRoot(Transform player, string screenName, float surfaceOffset, string rootName, out Transform root, out Vector2 size)
    {
        root = null;
        size = default;

        SkinnedMeshRenderer screen = player.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(r => r.name == screenName);
        if (screen == null)
            return false;
        Measure(screen.bounds, PlayerBody.HeadPosition(player), out Vector3 center, out Vector3 normal, out size);

        root = new GameObject(rootName).transform;
        root.SetPositionAndRotation(center + normal * surfaceOffset, Quaternion.LookRotation(-normal, Vector3.up));

        Transform head = screen.bones.FirstOrDefault(bone => bone != null && bone.name == PlayerBody.HeadBoneName);
        root.SetParent(head != null ? head : screen.transform, true);
        return true;
    }

    /// <summary>
    /// 画面の描画範囲から、手前の面の中心・正面の向き・大きさ（横と縦）を求める。
    /// 正面は頭の中心から画面の中心へ向かう水平の向きとする（モデルは拡大されているため頂点からは求めない）。
    /// 画面は中央が手前へ膨らんでいるため、範囲の厚みの半分だけ手前を面とする。
    /// </summary>
    static void Measure(Bounds bounds, Vector3 head, out Vector3 center, out Vector3 normal, out Vector2 size)
    {
        Vector3 toScreen = bounds.center - head;
        toScreen.y = 0f;
        normal = toScreen.sqrMagnitude > 0f ? toScreen.normalized : Vector3.forward;
        float halfDepth = Mathf.Abs(bounds.extents.x * normal.x) + Mathf.Abs(bounds.extents.z * normal.z);
        center = bounds.center + normal * halfDepth;
        size = new Vector2(Mathf.Max(bounds.size.x, bounds.size.z), bounds.size.y);
    }
}
