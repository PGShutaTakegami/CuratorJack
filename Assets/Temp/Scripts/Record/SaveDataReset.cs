using UnityEngine;

/// <summary>
/// 保存しているデータ（実績・戦績・名前・アイコン・設定）をすべて消す（開発用）。
/// 覚えている値も一緒に消すため、起動し直さなくても反映される。
/// </summary>
public static class SaveDataReset
{
    public static void ResetAll()
    {
        AchievementService.ResetAll();
        PlayerRecordService.Clear();
        PlayerNameContext.Clear();
        PlayerIconStore.Clear();
        GameSettingsStore.Clear();
        PlayerPrefs.Save();
        Debug.LogWarning("[SaveData] 実績・戦績・名前・アイコン・設定をすべてリセットしました");
    }
}
