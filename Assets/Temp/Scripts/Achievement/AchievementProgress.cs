using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 解除した実績と解除した日時。JsonUtility で保存できる形にしている。
/// 日時は Steam の解除日時と同じ UTC の Unix 秒で持ち、取り出すときにローカル時刻へ直す。
/// </summary>
[Serializable]
public sealed class AchievementProgress
{
    [Serializable]
    struct Record
    {
        public string id;
        public long unlockedAtUnix;
    }

    [SerializeField] private List<Record> records = new List<Record>();

    public bool IsUnlocked(string id) => IndexOf(id) >= 0;

    public bool TryGetUnlockedAt(string id, out DateTime unlockedAt)
    {
        int index = IndexOf(id);
        unlockedAt = index >= 0 ? DateTimeOffset.FromUnixTimeSeconds(records[index].unlockedAtUnix).LocalDateTime : default;
        return index >= 0;
    }

    /// <summary>解除する。ID が空か、もう解除していれば false</summary>
    public bool TryUnlock(string id, DateTime unlockedAt)
    {
        if (string.IsNullOrEmpty(id) || IsUnlocked(id))
            return false;

        records.Add(new Record { id = id, unlockedAtUnix = new DateTimeOffset(unlockedAt).ToUnixTimeSeconds() });
        return true;
    }

    public string ToJson() => JsonUtility.ToJson(this);

    /// <summary>保存した文字列から戻す。空や壊れた文字列なら、何も解除していない状態を返す</summary>
    public static AchievementProgress FromJson(string json)
    {
        var progress = new AchievementProgress();
        if (string.IsNullOrEmpty(json))
            return progress;

        try
        {
            JsonUtility.FromJsonOverwrite(json, progress);
        }
        catch (ArgumentException)
        {
            return new AchievementProgress();
        }
        progress.records ??= new List<Record>();
        return progress;
    }

    int IndexOf(string id)
    {
        for (int i = 0; i < records.Count; i++)
        {
            if (records[i].id == id)
                return i;
        }
        return -1;
    }
}
