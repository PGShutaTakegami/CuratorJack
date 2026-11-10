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

    /// <summary>Set した名前をすぐディスクへ書き出す（強制終了しても消えないように）</summary>
    public static void Flush() => PlayerPrefs.Save();

    /// <summary>保存した名前を消す</summary>
    public static void Clear()
    {
        PlayerPrefs.DeleteKey(SaveKey);
        current = null;
    }

    /// <summary>タグになる文字・改行・前後の空白を除き、長すぎる名前を切り詰める。サーバーも受け取った名前をこれで整える。</summary>
    public static string Sanitize(string name)
    {
        if (string.IsNullOrEmpty(name))
            return string.Empty;

        string trimmed = RichTextGuard.Strip(name).Replace("\r", string.Empty).Replace("\n", string.Empty).Trim();
        return trimmed.Length > MaxLength ? trimmed.Substring(0, MaxLength) : trimmed;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        current = null;
    }
}
