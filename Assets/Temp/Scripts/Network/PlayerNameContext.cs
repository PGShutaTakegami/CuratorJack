using UnityEngine;

/// <summary>
/// ロビーで入力した自分の名前。次に起動したときも同じ名前を使えるよう保存しておく。
/// </summary>
public static class PlayerNameContext
{
    public const int MaxLength = 12;

    const string SaveKey = "CJ.PlayerName";

    static string current;

    public static string Current => current ??= Sanitize(PlayerPrefs.GetString(SaveKey, string.Empty));

    public static void Set(string name)
    {
        current = Sanitize(name);
        PlayerPrefs.SetString(SaveKey, current);
    }

    /// <summary>前後の空白と改行を除き、長すぎる名前を切り詰める。</summary>
    public static string Sanitize(string name)
    {
        if (string.IsNullOrEmpty(name))
            return string.Empty;

        string trimmed = name.Replace("\r", string.Empty).Replace("\n", string.Empty).Trim();
        return trimmed.Length > MaxLength ? trimmed.Substring(0, MaxLength) : trimmed;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        current = null;
    }
}
