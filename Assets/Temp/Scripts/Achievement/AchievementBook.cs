using System;
using System.Collections.Generic;

/// <summary>
/// 実績の定義と解除の状況をまとめ、解除を受け付ける。定義にない ID と、解除済みの実績は無視する。
/// 新しく解除したときだけ Unlocked を出す（保存は呼ぶ側が行う）。
/// </summary>
public sealed class AchievementBook
{
    readonly IReadOnlyList<AchievementDefinition> definitions;
    readonly AchievementProgress progress;
    readonly Func<DateTime> clock;

    public AchievementBook(IReadOnlyList<AchievementDefinition> definitions, AchievementProgress progress, Func<DateTime> clock)
    {
        this.definitions = definitions ?? Array.Empty<AchievementDefinition>();
        this.progress = progress ?? new AchievementProgress();
        this.clock = clock;
    }

    public event Action<AchievementDefinition> Unlocked;

    public IReadOnlyList<AchievementDefinition> Definitions => definitions;

    public AchievementProgress Progress => progress;

    public int UnlockedCount
    {
        get
        {
            int count = 0;
            foreach (AchievementDefinition definition in definitions)
            {
                if (progress.IsUnlocked(definition.Id))
                    count++;
            }
            return count;
        }
    }

    public bool IsUnlocked(string id) => progress.IsUnlocked(id);

    public bool TryGetUnlockedAt(string id, out DateTime unlockedAt) => progress.TryGetUnlockedAt(id, out unlockedAt);

    /// <summary>新しく解除したら true</summary>
    public bool Unlock(string id)
    {
        AchievementDefinition definition = Find(id);
        if (definition == null || !progress.TryUnlock(id, clock()))
            return false;

        Unlocked?.Invoke(definition);
        return true;
    }

    public AchievementDefinition Find(string id)
    {
        foreach (AchievementDefinition definition in definitions)
        {
            if (definition.Id == id)
                return definition;
        }
        return null;
    }
}
