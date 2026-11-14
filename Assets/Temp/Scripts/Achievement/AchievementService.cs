using System;
using UnityEngine;

/// <summary>
/// どこからでも実績を解除・参照できる窓口。定義は Resources/AchievementCatalog、解除の状況は PlayerPrefs に JSON で保存する。
/// 新しく解除したときだけ保存して Unlocked を出す（通知は AchievementToast が受け取る）。
/// Steam などの外部の実績は RegisterPlatform で登録すると、解除が伝わる。
/// </summary>
public static class AchievementService
{
    const string PrefsKey = "CJ.Achievements";
    const string CatalogPath = "AchievementCatalog";

    static AchievementCatalog catalog;
    static AchievementBook book;
    static AchievementPlatformRelay relay = new AchievementPlatformRelay();

    public static event Action<AchievementDefinition> Unlocked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        catalog = null;
        book = null;
        relay = new AchievementPlatformRelay();
        Unlocked = null;
    }

    public static AchievementCatalog Catalog
    {
        get
        {
            if (catalog == null)
                catalog = Resources.Load<AchievementCatalog>(CatalogPath);
            return catalog;
        }
    }

    public static AchievementBook Book
    {
        get
        {
            if (book == null)
            {
                AchievementCatalog source = Catalog;
                if (source == null)
                    Debug.LogWarning(GameText.Format("log.resource_missing", "[Achievement]", CatalogPath));
                book = new AchievementBook(source != null ? source.Achievements : null, AchievementProgress.FromJson(PlayerPrefs.GetString(PrefsKey, string.Empty)), () => DateTime.Now);
                book.Unlocked += HandleUnlocked;
                relay.Bind(book);
            }
            return book;
        }
    }

    public static bool IsUnlocked(string id) => Book.IsUnlocked(id);

    public static bool Unlock(string id) => Book.Unlock(id);

    /// <summary>外部の実績を登録する。登録した時点で解除済みの実績もすぐに伝える</summary>
    public static void RegisterPlatform(IAchievementPlatform platform)
    {
        _ = Book;
        relay.Register(platform);
    }

    public static void UnregisterPlatform(IAchievementPlatform platform) => relay.Unregister(platform);

    /// <summary>解除の状況をすべて消す（開発用）</summary>
    public static void ResetAll()
    {
        PlayerPrefs.DeleteKey(PrefsKey);
        PlayerPrefs.Save();
        if (book != null)
            book.Unlocked -= HandleUnlocked;
        book = null;
        relay.Bind(null);
    }

    static void HandleUnlocked(AchievementDefinition definition)
    {
        PlayerPrefs.SetString(PrefsKey, book.Progress.ToJson());
        PlayerPrefs.Save();
        Debug.Log(GameText.Format("log.achievement.unlocked", definition.Id));
        Unlocked?.Invoke(definition);
    }
}
