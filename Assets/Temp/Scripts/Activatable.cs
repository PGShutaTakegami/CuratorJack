using System.Collections.Generic;
using UnityEngine;

/// <summary>ゲームに出すかどうかをデータで切り替えられるもの（カード・アイテム）。追加・撤去はこのフラグで行う</summary>
public interface IActivatable
{
    /// <summary>false ならゲームに出てこない（山札・インフォメーション・チュートリアルから外れる）</summary>
    bool Active { get; }
}

public static class Activatable
{
    public static bool IsActive<T>(T entry) where T : Object, IActivatable => entry != null && entry.Active;

    /// <summary>空の要素と Active でないものを除き、元の並びのまま返す</summary>
    public static List<T> ActiveOnly<T>(IEnumerable<T> source) where T : Object, IActivatable
    {
        var result = new List<T>();
        if (source == null)
            return result;

        foreach (T entry in source)
        {
            if (IsActive(entry))
                result.Add(entry);
        }
        return result;
    }
}
