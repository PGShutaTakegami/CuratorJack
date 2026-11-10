using EnvironmentSwitcher;
using UnityEngine;

/// <summary>
/// 左下の DEBUG パネル（開発用の環境でだけ出る）に「セーブデータ」の項目を足す。
/// 今の戦績と実績の解除数を出し（パネルを作り直したときの値。「表示を更新」で作り直す）、保存しているデータをすべてリセットできる。
/// </summary>
public static class SaveDataDebugSection
{
    const string SectionId = "cj.savedata";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Register()
    {
        DevDebugRegistry.RegisterSection(SectionId, "セーブデータ", Build, 10);
    }

    static void Build(DevDebugMenuContext context)
    {
        PlayerRecord record = PlayerRecordService.Current;
        AchievementBook book = AchievementService.Book;
        context.AddLabel($"プレイ {record.GamesPlayed} 回 / 勝利 {record.Wins} 回", 15);
        context.AddLabel($"落札 {record.Purchases} / 売れた出品 {record.Sales} / アイテム {record.ItemsUsed}", 15);
        context.AddLabel($"実績 {book.UnlockedCount} / {book.Definitions.Count}", 15);
        context.AddButton("RefreshSaveData", "表示を更新", new Color(0.25f, 0.4f, 0.65f, 1f), Register);
        context.AddButton("ResetSaveData", "セーブデータをすべてリセット", new Color(0.7f, 0.25f, 0.1f, 1f), () =>
        {
            SaveDataReset.ResetAll();
            Register();
        });
    }
}
