using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 実績の解除を、登録した外部の実績（Steam など）へ伝える。
/// 登録した時点で解除済みの実績もまとめて伝えるので、外部につながる前の解除も取りこぼさない。
/// </summary>
public sealed class AchievementPlatformRelay
{
    readonly List<IAchievementPlatform> platforms = new List<IAchievementPlatform>();
    AchievementBook book;

    /// <summary>解除を見張る実績帳を差し替え、解除済みのものを登録先へ伝える</summary>
    public void Bind(AchievementBook target)
    {
        if (book != null)
            book.Unlocked -= Forward;
        book = target;
        if (book == null)
            return;

        book.Unlocked += Forward;
        foreach (IAchievementPlatform platform in platforms)
            SendUnlocked(platform);
    }

    public void Register(IAchievementPlatform platform)
    {
        if (platform == null || platforms.Contains(platform))
            return;

        platforms.Add(platform);
        SendUnlocked(platform);
    }

    public void Unregister(IAchievementPlatform platform) => platforms.Remove(platform);

    void SendUnlocked(IAchievementPlatform platform)
    {
        if (book == null)
            return;

        foreach (AchievementDefinition definition in book.Definitions)
        {
            if (book.IsUnlocked(definition.Id))
                Send(platform, definition);
        }
    }

    void Forward(AchievementDefinition definition)
    {
        foreach (IAchievementPlatform platform in platforms)
            Send(platform, definition);
    }

    static void Send(IAchievementPlatform platform, AchievementDefinition definition)
    {
        try
        {
            platform.Unlock(definition);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }
}
