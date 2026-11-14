using System;
using UnityEngine;

/// <summary>
/// どこからでも戦績を増やし・参照できる窓口。PlayerPrefs に JSON で保存し、変わるたびに書き出す。
/// </summary>
public static class PlayerRecordService
{
    const string PrefsKey = "CJ.PlayerRecord";

    static PlayerRecord current;

    /// <summary>今の戦績（変えるときは Change を使う）</summary>
    public static PlayerRecord Current => current ??= PlayerRecord.FromJson(PlayerPrefs.GetString(PrefsKey, string.Empty));

    /// <summary>戦績を変えて保存する</summary>
    public static void Change(Action<PlayerRecord> change)
    {
        change(Current);
        PlayerPrefs.SetString(PrefsKey, current.ToJson());
        PlayerPrefs.Save();
    }

    /// <summary>戦績をすべて消す</summary>
    public static void Clear()
    {
        PlayerPrefs.DeleteKey(PrefsKey);
        PlayerPrefs.Save();
        current = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        current = null;
    }
}
