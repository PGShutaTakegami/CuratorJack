/// <summary>
/// Steam などゲームの外の実績へ解除を伝える先。AchievementService.RegisterPlatform で登録する。
/// Steam なら SteamUserStats.SetAchievement(definition.SteamApiName) → StoreStats() を呼ぶ実装にする。
/// </summary>
public interface IAchievementPlatform
{
    /// <summary>解除を伝える。もう解除済みのものが来ても問題ないようにする</summary>
    void Unlock(AchievementDefinition definition);
}
