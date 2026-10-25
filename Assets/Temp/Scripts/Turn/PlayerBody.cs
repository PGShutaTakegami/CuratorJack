using UnityEngine;

/// <summary>
/// プレイヤーのモデルから、表示やカメラの目印になる位置を求める。
/// </summary>
public static class PlayerBody
{
    public const string HeadBoneName = "DEF.HEAD";

    /// <summary>頭の位置。頭が見つからなければ見た目の中心、見た目もなければ足元。</summary>
    public static Vector3 HeadPosition(Transform player, string headName = HeadBoneName)
    {
        Transform head = FindDescendant(player, headName);
        if (head != null)
            return head.position;

        Renderer[] renderers = player.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return player.position;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return bounds.center;
    }

    /// <summary>頭のボーン。見つからなければ null（毎フレーム位置を追うときに、探し直さずに済むように使う）</summary>
    public static Transform FindHead(Transform player, string headName = HeadBoneName)
    {
        return FindDescendant(player, headName);
    }

    static Transform FindDescendant(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name)
                return child;

            Transform found = FindDescendant(child, name);
            if (found != null)
                return found;
        }
        return null;
    }
}
