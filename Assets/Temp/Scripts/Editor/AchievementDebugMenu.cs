using UnityEditor;

/// <summary>開発用：実績の解除状況を消す・すべて解除する</summary>
static class AchievementDebugMenu
{
    [MenuItem("CJ/Achievements/Reset All")]
    static void ResetAll() => AchievementService.ResetAll();

    [MenuItem("CJ/Achievements/Unlock All")]
    static void UnlockAll()
    {
        foreach (AchievementDefinition definition in AchievementService.Book.Definitions)
            AchievementService.Unlock(definition.Id);
    }
}
