using System;
using UnityEngine;

/// <summary>
/// 設定を保存し、次に起動したときも同じ設定を使えるようにする。
/// Current は複製を返すため、変えた値は Save で戻す。
/// </summary>
public static class GameSettingsStore
{
    const string SaveKey = "CJ.GameSettings";

    static GameSettings current;

    public static GameSettings Current => (current ??= Load()).Clone();

    /// <summary>設定が保存された（ゲーム中に設定を使う側が、変わった値を反映するため）。保存した値の複製を渡す</summary>
    public static event Action<GameSettings> Saved;

    public static void Save(GameSettings settings)
    {
        current = settings.Clone();
        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(current));
        Saved?.Invoke(current.Clone());
    }

    /// <summary>保存した設定を消して初期値に戻す（使う側へは Saved で初期値を知らせる）</summary>
    public static void Clear()
    {
        PlayerPrefs.DeleteKey(SaveKey);
        current = CreateDefault();
        Saved?.Invoke(current.Clone());
    }

    /// <summary>まとめてディスクへ書き出す（音量バーを動かしている間は書き出さず、画面を閉じるときに呼ぶ）</summary>
    public static void Flush() => PlayerPrefs.Save();

    static GameSettings Load()
    {
        string json = PlayerPrefs.GetString(SaveKey, string.Empty);
        if (string.IsNullOrEmpty(json))
            return CreateDefault();

        var settings = new GameSettings();
        JsonUtility.FromJsonOverwrite(json, settings);
        return settings;
    }

    /// <summary>まだ保存が無いときの設定。言語は PC の言語に合わせる</summary>
    static GameSettings CreateDefault() => new GameSettings { Language = LanguageCodes.FromSystem(Application.systemLanguage) };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        current = null;
        Saved = null;
    }
}
